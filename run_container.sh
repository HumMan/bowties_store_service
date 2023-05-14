#!/bin/bash
imageName=bowties_image
containerName=bowties

docker build -t $imageName -f Dockerfile  .

echo Delete old container...
docker stop $containerName
docker rm -f $containerName

echo Run new container...
docker run -d --link mongodb-server -p 127.0.0.1:5000:80 --name $containerName $imageName

#sudo docker run -d --name bowties --memory=400m --restart unless-stopped -p 127.0.0.1:58943:12000 -e PORT='12000' bowtiesstoreservice 
