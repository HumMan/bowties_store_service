using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Configuration;
using Microsoft.IdentityModel.Tokens;
using store_service.App.Repository;
using System;
using System.Collections.Generic;
using System.IdentityModel.Tokens.Jwt;
using System.Linq;
using System.Security.Claims;
using System.Text;
using System.Threading.Tasks;
using Model = store_service.App.Model;
using store_service.App.Model.Converters;

namespace store_service.Controllers
{
    [Authorize(Roles = "Admin")]
    [Produces("application/json")]
    [Route("api/[controller]")]
    public class UserController : Controller
    {
        private readonly IConfiguration Configuration;
        private readonly IUsersRepository Repository;
        public UserController(IConfiguration configuration, IUsersRepository repository)
        {
            Configuration = configuration;
            Repository = repository;
        }
        [Authorize(Roles = "Admin")]
        [HttpGet]
        public async Task<Model.Web.User[]> GetUsersList()
        {
            var result = (await Repository.GetUsers()).Select(x => x.ToWeb()).ToArray();
            return result;
        }
        //[HttpPost]
        //public async Task<IActionResult> UpdateUser([FromBody] Model.Web.User user)
        //{
        //    await Repository.UpdateUser(user.ToData());
        //    return Ok();
        //}
    }
}