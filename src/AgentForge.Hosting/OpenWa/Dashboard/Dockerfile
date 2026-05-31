ARG OPENWA_VERSION=0.1.6

FROM alpine:3.22 AS source
ARG OPENWA_VERSION
RUN apk add --no-cache curl tar
WORKDIR /src
RUN case "$OPENWA_VERSION" in \
      main) archive_url="https://codeload.github.com/rmyndharis/OpenWA/tar.gz/refs/heads/main" ;; \
      v*) archive_url="https://codeload.github.com/rmyndharis/OpenWA/tar.gz/refs/tags/${OPENWA_VERSION}" ;; \
      *) archive_url="https://codeload.github.com/rmyndharis/OpenWA/tar.gz/refs/tags/v${OPENWA_VERSION}" ;; \
    esac \
 && curl -fsSL "$archive_url" -o openwa.tar.gz \
 && tar -xzf openwa.tar.gz -C /src --strip-components=1

FROM node:22-alpine AS build
WORKDIR /app
COPY --from=source /src/dashboard/package*.json ./
RUN npm ci
COPY --from=source /src/dashboard/ ./
RUN npm run build

FROM nginx:1.29-alpine
COPY --from=build /app/dist /usr/share/nginx/html
COPY nginx.conf /etc/nginx/conf.d/default.conf
EXPOSE 80
