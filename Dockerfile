FROM mcr.microsoft.com/dotnet/core/aspnet:2.2 AS base
WORKDIR /app

FROM mcr.microsoft.com/dotnet/core/sdk:2.2 AS build
WORKDIR /
COPY NuGet.config ./
COPY *.sln ./
COPY src/Skoruba.IdentityServer4.Admin/Skoruba.IdentityServer4.Admin.csproj src/Skoruba.IdentityServer4.Admin/
COPY src/Skoruba.IdentityServer4.Admin.Api/Skoruba.IdentityServer4.Admin.Api.csproj src/Skoruba.IdentityServer4.Admin.Api/
COPY src/Skoruba.IdentityServer4.Admin.BusinessLogic/Skoruba.IdentityServer4.Admin.BusinessLogic.csproj src/Skoruba.IdentityServer4.Admin.BusinessLogic/
COPY src/Skoruba.IdentityServer4.Admin.BusinessLogic.Identity/Skoruba.IdentityServer4.Admin.BusinessLogic.Identity.csproj src/Skoruba.IdentityServer4.Admin.BusinessLogic.Identity/
COPY src/Skoruba.IdentityServer4.Admin.BusinessLogic.Shared/Skoruba.IdentityServer4.Admin.BusinessLogic.Shared.csproj src/Skoruba.IdentityServer4.Admin.BusinessLogic.Shared/
COPY src/Skoruba.IdentityServer4.Admin.EntityFramework/Skoruba.IdentityServer4.Admin.EntityFramework.csproj src/Skoruba.IdentityServer4.Admin.EntityFramework/
COPY src/Skoruba.IdentityServer4.Admin.EntityFramework.Shared/Skoruba.IdentityServer4.Admin.EntityFramework.Shared.csproj src/Skoruba.IdentityServer4.Admin.EntityFramework.Shared/
COPY src/Skoruba.IdentityServer4.Admin.EntityFramework.Extensions/Skoruba.IdentityServer4.Admin.EntityFramework.Extensions.csproj src/Skoruba.IdentityServer4.Admin.EntityFramework.Extensions/
COPY src/Skoruba.IdentityServer4.Admin.EntityFramework.Identity/Skoruba.IdentityServer4.Admin.EntityFramework.Identity.csproj src/Skoruba.IdentityServer4.Admin.EntityFramework.Identity/
COPY src/Skoruba.IdentityServer4.STS.Identity/Skoruba.IdentityServer4.STS.Identity.csproj src/Skoruba.IdentityServer4.STS.Identity/
COPY tests/Skoruba.IdentityServer4.Admin.IntegrationTests/Skoruba.IdentityServer4.Admin.IntegrationTests.csproj tests/Skoruba.IdentityServer4.Admin.IntegrationTests/
COPY tests/Skoruba.IdentityServer4.Admin.UnitTests/Skoruba.IdentityServer4.Admin.UnitTests.csproj tests/Skoruba.IdentityServer4.Admin.UnitTests/
COPY tests/Skoruba.IdentityServer4.STS.Identity.IntegrationTests/Skoruba.IdentityServer4.STS.Identity.IntegrationTests.csproj tests/Skoruba.IdentityServer4.STS.Identity.IntegrationTests/

RUN dotnet restore
COPY . .
WORKDIR src/Skoruba.IdentityServer4.Admin
RUN dotnet build -c Release -o /app

FROM build AS publish
RUN dotnet publish -c Release -o /app

FROM base as final
WORKDIR /app
COPY --from=publish /app .

ENTRYPOINT ["dotnet", "Skoruba.IdentityServer4.Admin.dll"]
#HEALTHCHECK CMD curl --fail http://127.0.0.1:80/ || exit 1