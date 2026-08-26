FROM node:24-alpine AS build
WORKDIR /app

COPY src/CloudKnowledge.Web/package.json ./
COPY src/CloudKnowledge.Web/package-lock.json ./
RUN npm ci

COPY src/CloudKnowledge.Web/ .
RUN npm run build

FROM nginxinc/nginx-unprivileged:1.29-alpine AS final

COPY src/CloudKnowledge.Web/nginx.conf /etc/nginx/conf.d/default.conf
COPY --from=build /app/dist/cloudknowledge-web/browser /usr/share/nginx/html

EXPOSE 8080
