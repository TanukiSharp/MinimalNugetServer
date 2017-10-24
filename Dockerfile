FROM microsoft/aspnetcore-build:2.0.0-stretch AS publish

ENV ASPNETCORE_URLS http://*:80
ENV ASPNETCORE_ENVIRONMENT "Production"

COPY . /src
WORKDIR /src/MinimalNugetServer

RUN dotnet restore
RUN dotnet publish --output /src/out

FROM microsoft/aspnetcore:2.0.0-stretch

ENV ASPNETCORE_URLS http://*:80
ENV ASPNETCORE_ENVIRONMENT "Production"

WORKDIR /dotnetapp
COPY --from=publish /src/out .

EXPOSE 80/tcp

ENTRYPOINT ["dotnet", "MinimalNugetServer.dll"]