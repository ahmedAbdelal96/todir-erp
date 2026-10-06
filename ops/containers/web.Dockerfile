FROM node:24.13.0-alpine AS build
WORKDIR /app
COPY package.json pnpm-lock.yaml pnpm-workspace.yaml ./
COPY src/frontend/apps/todir-web/package.json src/frontend/apps/todir-web/
RUN corepack enable && pnpm install --frozen-lockfile
COPY src/frontend/apps/todir-web src/frontend/apps/todir-web
RUN pnpm --dir src/frontend/apps/todir-web build

FROM nginx:1.27-alpine
COPY ops/containers/web.conf /etc/nginx/conf.d/default.conf
COPY --from=build /app/src/frontend/apps/todir-web/dist /usr/share/nginx/html
EXPOSE 8080
