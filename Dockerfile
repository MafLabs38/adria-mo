# syntax=docker/dockerfile:1

# The back end of Adria MO (spec §10.3): the administration API, its database and the publication of the public snapshot.
# The public part is a static site served by a CDN, not by this image.
# The base images are pinned by digest (the tag says what they are, the digest what they were); Dependabot moves both
# (an exception to SonarQube's docker:S8431, stated in ci.yml).

FROM --platform=$BUILDPLATFORM mcr.microsoft.com/dotnet/sdk:10.0@sha256:de4fe90a98845d75910b9769662465c84de1a934665c431279d1382b2bddc02b AS build
ARG VERSION=0.0.0-dev
WORKDIR /build
COPY global.json Directory.Build.props Directory.Packages.props ./
# The projects that ship, copied alone first so that packages are restored in a layer a source change does not invalidate.
COPY src/AdriaMo.Api/AdriaMo.Api.csproj src/AdriaMo.Api/
COPY src/AdriaMo.Core/AdriaMo.Core.csproj src/AdriaMo.Core/
RUN dotnet restore src/AdriaMo.Api/AdriaMo.Api.csproj
COPY src/AdriaMo.Api ./src/AdriaMo.Api
COPY src/AdriaMo.Core ./src/AdriaMo.Core
RUN dotnet publish src/AdriaMo.Api/AdriaMo.Api.csproj -c Release --no-restore -o /app -p:Version=${VERSION}

# Chiseled image: no shell, no package manager, non-root user by default.
FROM mcr.microsoft.com/dotnet/aspnet:10.0-noble-chiseled@sha256:7c625e44e5d588b4d7b2a913e94ca4bd5098ce47442da404829e4c7936a764e1
WORKDIR /app
COPY --from=build /app ./
# No data volume yet: nothing is stored. The storage vertical adds it (a folder the application's user can write, which
# SonarQube reports as hotspot docker:S6504 for the maintainer to review).
ENV ASPNETCORE_HTTP_PORTS=8080
# The app user of the chiseled image, stated so that nothing reads this image as running as root.
USER 1654:1654
EXPOSE 8080
ENTRYPOINT ["dotnet", "AdriaMo.Api.dll"]
