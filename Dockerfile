# One container: the ASP.NET Core API serves the React build from wwwroot.

# 1) Build the React app (vite outputs to ../src/EmployeeRequests.Api/wwwroot)
FROM node:22-alpine AS web
WORKDIR /repo/web
COPY web/package.json web/package-lock.json ./
RUN npm ci
COPY web/ ./
RUN npm run build

# 2) Publish the API with the React build inside it
FROM mcr.microsoft.com/dotnet/sdk:10.0 AS api
WORKDIR /repo
COPY src/EmployeeRequests.Api/EmployeeRequests.Api.csproj src/EmployeeRequests.Api/
RUN dotnet restore src/EmployeeRequests.Api/EmployeeRequests.Api.csproj
COPY src/ src/
COPY --from=web /repo/src/EmployeeRequests.Api/wwwroot src/EmployeeRequests.Api/wwwroot
RUN dotnet publish src/EmployeeRequests.Api/EmployeeRequests.Api.csproj -c Release -o /app --no-restore

# 3) Runtime image
FROM mcr.microsoft.com/dotnet/aspnet:10.0
WORKDIR /app
COPY --from=api /app .
ENV ASPNETCORE_ENVIRONMENT=Production
EXPOSE 10000
# Render injects PORT; default to 10000 when running elsewhere.
CMD ["sh", "-c", "ASPNETCORE_HTTP_PORTS=${PORT:-10000} exec dotnet EmployeeRequests.Api.dll"]
