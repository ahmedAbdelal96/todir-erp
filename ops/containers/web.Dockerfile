FROM node:24.13.0-alpine AS build
WORKDIR /app
COPY package.json pnpm-lock.yaml pnpm-workspace.yaml tsconfig.json ./
COPY src/frontend/apps/todir-web/package.json src/frontend/apps/todir-web/
RUN corepack enable && pnpm install --frozen-lockfile
COPY src/frontend/apps/todir-web src/frontend/apps/todir-web
RUN cd src/frontend/apps/todir-web && pnpm build

FROM nginx:1.29-alpine3.22
RUN apk upgrade --no-cache
COPY ops/containers/web.conf /etc/nginx/conf.d/default.conf
COPY --from=build /app/src/frontend/apps/todir-web/dist /usr/share/nginx/html
EXPOSE 8080
