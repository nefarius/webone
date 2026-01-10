# syntax=docker/dockerfile:1
FROM mcr.microsoft.com/dotnet/sdk:8.0 AS build
WORKDIR /src
COPY . .
RUN dotnet publish -c ReleaseSC -r linux-x64 --self-contained true -o /out

FROM debian:bookworm-slim
RUN apt-get update \
    && apt-get install -y --no-install-recommends \
        ca-certificates \
        ffmpeg \
        imagemagick \
        libgssapi-krb5-2 \
        libicu72 \
        libssl3 \
        yt-dlp \
    && rm -rf /var/lib/apt/lists/*

RUN useradd --system --uid 10001 --home /app --shell /usr/sbin/nologin webone

WORKDIR /app
COPY --from=build /out/ /app/
RUN rm -f /app/webone.conf /app/codepage.conf /app/escargot.conf /app/openssl_webone.cnf

RUN mkdir -p /etc/webone.conf.d /var/log \
    && touch /var/log/webone.log /etc/webone.conf.d/ssl.crt /etc/webone.conf.d/ssl.key \
    && chmod 666 /var/log/webone.log /etc/webone.conf.d/ssl.crt /etc/webone.conf.d/ssl.key \
    && chown -R webone:webone /app /etc/webone.conf.d /var/log

COPY --from=build /out/webone.conf /etc/webone.conf
COPY --from=build /out/codepage.conf /etc/webone.conf.d/codepage.conf
COPY --from=build /out/escargot.conf /etc/webone.conf.d/escargot.conf
COPY --from=build /out/openssl_webone.cnf /etc/webone.conf.d/openssl_webone.cnf

ENV OPENSSL_CONF=/etc/webone.conf.d/openssl_webone.cnf

VOLUME ["/etc/webone.conf.d", "/var/log"]
EXPOSE 8080

USER webone
ENTRYPOINT ["/app/webone"]
