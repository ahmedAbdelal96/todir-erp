FROM node:24.13.0-alpine AS build
WORKDIR /app
COPY package.json pnpm-lock.yaml pnpm-workspace.yaml tsconfig.json ./
COPY src/frontend/package.json src/frontend/
RUN corepack enable && pnpm install --frozen-lockfile
COPY src/frontend src/frontend
RUN corepack enable && pnpm install --frozen-lockfile
RUN cd src/frontend && pnpm build

FROM nginx:1.29-alpine3.22
RUN apk upgrade --no-cache
COPY ops/containers/web.conf /etc/nginx/conf.d/default.conf
COPY --from=build /app/src/frontend/dist /usr/share/nginx/html
EXPOSE 8080
