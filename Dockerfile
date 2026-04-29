FROM mcr.microsoft.com/dotnet/sdk:8.0 AS build
WORKDIR /src
COPY . .
RUN dotnet restore "CertificatePortal/CertificatePortal.csproj"
RUN dotnet publish "CertificatePortal/CertificatePortal.csproj" -c Release -o /app/publish

FROM mcr.microsoft.com/dotnet/aspnet:8.0 AS final
WORKDIR /app

# Install modern dependencies for Puppeteer (Chromium) on Debian 12
RUN apt-get update && apt-get install -y \
    libnss3 \
    libnspr4 \
    libatk1.0-0 \
    libatk-bridge2.0-0 \
    libcups2 \
    libdrm2 \
    libxkbcommon0 \
    libxcomposite1 \
    libxdamage1 \
    libxext6 \
    libxfixes3 \
    libxrandr2 \
    libgbm1 \
    libasound2 \
    fonts-liberation \
    xdg-utils \
    wget \
    --no-install-recommends \
    && rm -rf /var/lib/apt/lists/*

COPY --from=build /app/publish .
ENV ASPNETCORE_ENVIRONMENT=Production
ENV UseMockData=true
ENTRYPOINT ["dotnet", "CertificatePortal.dll"]
