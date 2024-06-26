FROM mcr.microsoft.com/dotnet/aspnet:8.0 AS base
WORKDIR /app
EXPOSE 80
EXPOSE 443

FROM mcr.microsoft.com/dotnet/sdk:8.0 AS build
WORKDIR /src
COPY ["src/Skoruba.IdentityServer4.Admin/NuGet.config", "src/"]
COPY ["src/Skoruba.IdentityServer4.Admin/Skoruba.IdentityServer4.Admin.csproj", "src/Skoruba.IdentityServer4.Admin/"]
COPY ["src/Skoruba.IdentityServer4.Admin.EntityFramework.MySql/Skoruba.IdentityServer4.Admin.EntityFramework.MySql.csproj", "src/Skoruba.IdentityServer4.Admin.EntityFramework.MySql/"]
COPY ["src/Skoruba.IdentityServer4.Admin.EntityFramework.Identity/Skoruba.IdentityServer4.Admin.EntityFramework.Identity.csproj", "src/Skoruba.IdentityServer4.Admin.EntityFramework.Identity/"]
COPY ["src/Skoruba.IdentityServer4.Admin.EntityFramework/Skoruba.IdentityServer4.Admin.EntityFramework.csproj", "src/Skoruba.IdentityServer4.Admin.EntityFramework/"]
COPY ["src/Skoruba.IdentityServer4.Admin.EntityFramework.Extensions/Skoruba.IdentityServer4.Admin.EntityFramework.Extensions.csproj", "src/Skoruba.IdentityServer4.Admin.EntityFramework.Extensions/"]
COPY ["src/Skoruba.IdentityServer4.Admin.EntityFramework.Shared/Skoruba.IdentityServer4.Admin.EntityFramework.Shared.csproj", "src/Skoruba.IdentityServer4.Admin.EntityFramework.Shared/"]
COPY ["src/Skoruba.IdentityServer4.Admin.EntityFramework.Configuration/Skoruba.IdentityServer4.Admin.EntityFramework.Configuration.csproj", "src/Skoruba.IdentityServer4.Admin.EntityFramework.Configuration/"]
COPY ["src/Skoruba.IdentityServer4.Admin.BusinessLogic/Skoruba.IdentityServer4.Admin.BusinessLogic.csproj", "src/Skoruba.IdentityServer4.Admin.BusinessLogic/"]
COPY ["src/Skoruba.IdentityServer4.Admin.BusinessLogic.Shared/Skoruba.IdentityServer4.Admin.BusinessLogic.Shared.csproj", "src/Skoruba.IdentityServer4.Admin.BusinessLogic.Shared/"]
COPY ["src/Skoruba.IdentityServer4.Admin.EntityFramework.PostgreSQL/Skoruba.IdentityServer4.Admin.EntityFramework.PostgreSQL.csproj", "src/Skoruba.IdentityServer4.Admin.EntityFramework.PostgreSQL/"]
COPY ["src/Skoruba.IdentityServer4.Admin.BusinessLogic.Identity/Skoruba.IdentityServer4.Admin.BusinessLogic.Identity.csproj", "src/Skoruba.IdentityServer4.Admin.BusinessLogic.Identity/"]
COPY ["src/Skoruba.IdentityServer4.Admin.UI/Skoruba.IdentityServer4.Admin.UI.csproj", "src/Skoruba.IdentityServer4.Admin.UI/"]
COPY ["src/Skoruba.IdentityServer4.Shared.Configuration/Skoruba.IdentityServer4.Shared.Configuration.csproj", "src/Skoruba.IdentityServer4.Shared.Configuration/"]
COPY ["src/Skoruba.IdentityServer4.Shared/Skoruba.IdentityServer4.Shared.csproj", "src/Skoruba.IdentityServer4.Shared/"]
COPY ["src/Skoruba.IdentityServer4.Admin.EntityFramework.SqlServer/Skoruba.IdentityServer4.Admin.EntityFramework.SqlServer.csproj", "src/Skoruba.IdentityServer4.Admin.EntityFramework.SqlServer/"]
RUN dotnet restore "src/Skoruba.IdentityServer4.Admin/Skoruba.IdentityServer4.Admin.csproj"
COPY . .
WORKDIR "/src/src/Skoruba.IdentityServer4.Admin"
RUN dotnet build "Skoruba.IdentityServer4.Admin.csproj" -c Release -o /app/build

FROM build AS publish
RUN dotnet publish "Skoruba.IdentityServer4.Admin.csproj" -c Release -o /app/publish

FROM base AS final
WORKDIR /app
COPY --from=publish /app/publish .
ENV ASPNETCORE_FORWARDEDHEADERS_ENABLED=true
ENTRYPOINT ["dotnet", "Skoruba.IdentityServer4.Admin.dll"]