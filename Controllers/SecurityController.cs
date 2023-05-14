using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Configuration;
using Microsoft.IdentityModel.Tokens;
using MongoDB.Bson;
using Newtonsoft.Json;
using store_service.App.Repository;
using store_service.Helpers;
using System;
using System.Collections.Generic;
using System.IdentityModel.Tokens.Jwt;
using System.Linq;
using System.Net.Http;
using System.Security.Claims;
using System.Text;
using System.Threading.Tasks;
using Model = store_service.App.Model;

namespace store_service.Controllers
{

    public class TokenRequest
    {
        public string CaptchaToken { get; set; }
        public string Email { get; set; }
        public string Password { get; set; }
        public bool RememberMe { get; set; }
        /// <summary>
        /// Используется для обновления корзины
        /// </summary>
        public string TempSessionToken { get; set; }
    }

    public class ResetPasswordRequest
    {
        public string CaptchaToken { get; set; }
        public string Email { get; set; }
    }
    public class ResetPasswordChangeRequest
    {
        public string Token { get; set; }
        public string NewPassword { get; set; }
    }

    public class ChangePasswordRequest
    {
        public string OldPassword { get; set; }
        public string NewPassword { get; set; }
    }
    public class ValidateEmailRequest
    {
        public string Token { get; set; }
    }
    public class RegistrationRequest
    {
        public string CaptchaToken { get; set; }
        public string Username { get; set; }
        /// <summary>
        /// Не забываем везде где записывается преобразовывать в нижний регистр
        /// </summary>
        public string Email { get; set; }
        public string Password { get; set; }
        public bool RememberMe { get; set; }
        /// <summary>
        /// Используется для обновления корзины
        /// </summary>
        public string TempSessionToken { get; set; }
    }

    class TGoogleRecaptchaResult
    {
        public bool success { get; set; }
        public string challange_ts { get; set; }
        public string apk_package_name { get; set; }
        [JsonProperty("error-codes")]
        public List<string> error_codes { get; set; }
    }

    [Authorize()]
    [Produces("application/json")]
    [Route("api/[controller]")]
    public class LoginController : Controller
    {
        private const int SessionLifeWithRememberMe = 14; //days
        private const int SessionLifeWithoutRememberMe = 1; //days
        private const int EmailValidateResetExpiration = 10; //minutes
        private const int TempSessionLife = 90; //days

        private readonly IConfiguration _config;
        private readonly IUsersRepository UsersRepository;
        private readonly ICartsRepository CartsRepository;
        private readonly IEmailVerifyTicketRepository EmailVerifyTicketsRepo;
        private readonly IPasswordResetTicketRepository PasswordResetTickersRepo;
        private readonly IMailer _mailer;

        private readonly byte[] UserTicketSalt;

        public LoginController(
            IConfiguration configuration, 
            IUsersRepository repository,
            IEmailVerifyTicketRepository emailVerifyTicketsRepo,
            IPasswordResetTicketRepository passwordResetTickersRepo,
            IMailer mailer,
            ICartsRepository cartsRepository)
        {
            _config = configuration;
            UsersRepository = repository;
            CartsRepository = cartsRepository;
            EmailVerifyTicketsRepo = emailVerifyTicketsRepo;
            PasswordResetTickersRepo = passwordResetTickersRepo;
            _mailer = mailer;

            UserTicketSalt = Encoding.ASCII.GetBytes(configuration["UserTicketSalt"]);
        }

        #region private


        private static string[] WithInheritRoles(Model.UserRole role)
        {
            var names = Enum.GetNames(typeof(Model.UserRole));
            var result = new List<string>();
            for (int i = (int)role; i >= 1; i--)
            {
                result.Add(names[i-1]);
            }
            return result.ToArray();
        }

        private string CreateJwtToken(DateTime validThrough, string userId, string[] roles = null)
        {
            var claims = new List<Claim> {
                    new Claim(ClaimTypes.Name, userId),
                    new Claim(JwtRegisteredClaimNames.Jti, Guid.NewGuid().ToString()),
                };
            if (roles != null)
            {
                foreach (var role in roles)
                    claims.Add(new Claim(ClaimTypes.Role, role));
            }
            var key = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(_config["JwtBearer:JwtSecurityKey"]));
            var creds = new SigningCredentials(key, SecurityAlgorithms.HmacSha512Signature);

            var tokenDescriptor = new SecurityTokenDescriptor()
            {
                Subject = new ClaimsIdentity(claims),
                Issuer = _config["JwtBearer:ValidIssuer"],
                Audience = _config["JwtBearer:ValidAudience"],
                SigningCredentials = creds,                
                Expires= validThrough                
            };

            var tokenHandler = new JwtSecurityTokenHandler();
            var token = tokenHandler.CreateToken(tokenDescriptor);
            var jwtToken = tokenHandler.WriteToken(token);

            return jwtToken;
        }
        private async Task<bool> IsValidCaptcha(string captchaFormToken)
        {
            using (var httpClient = new HttpClient())
            {
                var content = new FormUrlEncodedContent(new[]{
                    new KeyValuePair<string,string>("secret",_config["Store:GoogleRecaptchaSecret"]),
                    new KeyValuePair<string,string>("response",captchaFormToken),
                });
                var response = await httpClient.PostAsync("https://www.google.com/recaptcha/api/siteverify", content);

                var captchaResultAsString = await response.Content.ReadAsStringAsync();
                var captchaResult = JsonConvert.DeserializeObject<TGoogleRecaptchaResult>(captchaResultAsString);
                return captchaResult.success;
            }
        }
        private async Task MergeCartsIfCan(TokenRequest request, Model.Data.User user)
        {
            if (!string.IsNullOrWhiteSpace(request.TempSessionToken))
            {
                var tempSessionInfo = new JwtSecurityTokenHandler().ReadJwtToken(request.TempSessionToken);
                var tempSessionUserId = tempSessionInfo.Claims.FirstOrDefault(i => i.Type == ClaimTypes.Name);
                await CartsRepository.MergeCarts(ObjectId.Parse(tempSessionUserId.Value), user.Id);
            }
        }


        private async Task<IActionResult> CreateValidateEmail(Model.Data.User user)
        {
            if (!user.RegisteredUser.IsEmailVerified &&
                user.RegisteredUser!=null && 
                user.RegisteredUser.Role == Model.UserRole.Regular)
            {
                var expirationDate = DateTime.UtcNow.AddMinutes(EmailValidateResetExpiration);
                UserHashing.CreateTokenAndHash(UserTicketSalt, out var token, out var tokenHash);
                await EmailVerifyTicketsRepo.CreateTicket(user.Id, user.Email, tokenHash, expirationDate);
                await _mailer.Send(user.Email, "Подтверждение почты", string.Format(
                    "<p>Вы были зарегистрированы на сайте <a href='{0}'>{3}</a></p>" +
                    "<p>Если вы не регистрировались на этом сайте, просьба проигнорировать это сообщение.</p>" +
                    "<p>Для подтверждения регистрации перейдите по <a href='{1}/account/email-validate/{2}'>этой ссылке</a></p>",

                    _config["Store:SiteDomain"],
                    _config["Store:SiteDomain"],
                    Base64UrlEncoder.Encode(token),
                    _config["Store:SiteName"]
                    ));
            }
            return Ok();
        }
        private static DateTime GetValidThrough(bool rememberMe)
        {
            return DateTime.UtcNow.AddHours(24 * (rememberMe ? SessionLifeWithRememberMe : SessionLifeWithoutRememberMe));
        }
        #endregion

        [Authorize(Roles = "Regular")]
        [HttpPost("changepass")]
        public async Task<IActionResult> ChangePassword([FromBody] ChangePasswordRequest request)
        {
            var userId = ObjectId.Parse(HttpContext.User.Identity.Name);
            var user = await UsersRepository.TryLogin(userId, request.OldPassword);

            if (user != null)
            {
                await UsersRepository.ChangeUserPassword(user.Email, userId, request.NewPassword);
                return Ok();
            }
            else
                return StatusCode(StatusCodes.Status401Unauthorized);
        }
        [AllowAnonymous]
        [HttpPost("createreset")]
        public async Task<IActionResult> CreatePasswordResetToken([FromBody] ResetPasswordRequest request)
        {
            if (!await IsValidCaptcha(request.CaptchaToken))
                return StatusCode(StatusCodes.Status401Unauthorized);

            var user = await UsersRepository.FindUser(request.Email.ToLower());
            if (user != null && user.RegisteredUser.IsEmailVerified 
                && user.RegisteredUser != null
                && user.RegisteredUser.Role == Model.UserRole.Regular)
            {
                var expirationDate = DateTime.UtcNow.AddMinutes(EmailValidateResetExpiration);
                UserHashing.CreateTokenAndHash(UserTicketSalt, out var token, out var tokenHash);
                await PasswordResetTickersRepo.CreateTicket(user.Id, user.Email, tokenHash, expirationDate);
                await _mailer.Send(user.Email, "Сброс пароля", string.Format(
                    "<p>Если вы не пытались сбросить пароль на сайте <a href='{0}'>{3}</a> просьба проигнорировать это сообщение.</p>" +
                    "<p>Для для продлжения сброса пароля перейдите по <a href='{1}/account/reset-validate/{2}'>этой ссылке</a></p>",

                    _config["Store:SiteDomain"],
                    _config["Store:SiteDomain"],
                    Base64UrlEncoder.Encode(token),
                    _config["Store:SiteName"]
                    ));
            }
            return Ok();
        }

        [AllowAnonymous]
        [HttpPost("temp_session")]
        public async Task<IActionResult> GetTempSession()
        {
            var user = await UsersRepository.CreateTempUser();

            await CartsRepository.CreateCart(user.Id);

            var validThrough = DateTime.UtcNow.AddDays(TempSessionLife);
            var token = CreateJwtToken(validThrough, user.Id.ToString());

            return Ok(new
            {
                token,
                role = 0,
            });
        }
        [AllowAnonymous]
        [HttpPost]
        public async Task<IActionResult> RequestToken([FromBody] TokenRequest request)
        {
            if (!await IsValidCaptcha(request.CaptchaToken))
                return StatusCode(StatusCodes.Status401Unauthorized);

            var user = await UsersRepository.TryLogin(request.Email, request.Password);
            IActionResult result;
            if (user != null)
            {
                //await MergeCartsIfCan(request, user);

                var validThrough = GetValidThrough(request.RememberMe);
                var token = CreateJwtToken(validThrough, user.Id.ToString(), WithInheritRoles(user.RegisteredUser.Role));

                result = Ok(new
                {
                    token,
                    role = (int)user.RegisteredUser.Role,
                });
            }
            else
                result = StatusCode(StatusCodes.Status401Unauthorized);

            return result;
        }
        [AllowAnonymous]
        [HttpPost("register")]
        public async Task<IActionResult> Register([FromBody] RegistrationRequest request)
        {
            if (!await IsValidCaptcha(request.CaptchaToken))
                return StatusCode(StatusCodes.Status401Unauthorized);

            if (await UsersRepository.FindUser(request.Email) != null)
                return StatusCode(StatusCodes.Status401Unauthorized);//TODO пользователь с таким email уже зарегистрирован

            var user = await UsersRepository.RegisterUser(request.Username, request.Password, request.Email);
            await CartsRepository.CreateCart(user.Id);

            IActionResult result;
            if (user != null)
            {
                var validThrough = GetValidThrough(request.RememberMe);
                var token = CreateJwtToken(validThrough, user.Id.ToString(), WithInheritRoles(user.RegisteredUser.Role));

                await CreateValidateEmail(user);

                result = Ok(new
                {
                    token,
                    role = (int)user.RegisteredUser.Role,
                });
            }
            else
                result = StatusCode(StatusCodes.Status500InternalServerError);

            return result;
        }        

        [AllowAnonymous]
        [HttpPost("validate")]
        public async Task<IActionResult> ValidateEmail([FromBody] ValidateEmailRequest request)
        {
            var tokenHash = Hashing.CreateHash(Base64UrlEncoder.Decode(request.Token), UserTicketSalt);
            var ticket = await EmailVerifyTicketsRepo.TryTicket(tokenHash);
            if (ticket != null)
            {
                var user = await UsersRepository.FindUser(ticket.Email);
                await UsersRepository.SetUserEmailValid(user.Id, true);
                return Ok();
            }
            else
                return BadRequest();
        }
        
        [AllowAnonymous]
        [HttpGet("canreset")]
        public async Task<IActionResult> CanResetPassword(string token)
        {
            var tokenHash = Hashing.CreateHash(Base64UrlEncoder.Decode(token), UserTicketSalt);
            if (await PasswordResetTickersRepo.IsValidTicket(tokenHash))
            {
                return Ok();
            }
            else
                return BadRequest();
        }
        [AllowAnonymous]
        [HttpPost("reset")]
        public async Task<IActionResult> ResetPassword([FromBody] ResetPasswordChangeRequest request)
        {
            var tokenHash = Hashing.CreateHash(Base64UrlEncoder.Decode(request.Token), UserTicketSalt);
            var ticket = await PasswordResetTickersRepo.TryTicket(tokenHash);
            if (ticket != null)
            {
                var user = await UsersRepository.FindUser(ticket.Email);
                await UsersRepository.ChangeUserPassword(user.Email, user.Id, request.NewPassword);
                return Ok();
            }
            else
                return BadRequest();
        }
    }
}