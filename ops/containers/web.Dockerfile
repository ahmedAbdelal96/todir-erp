FROM node:24.13.0-alpine AS build
WORKDIR /app
COPY package.json pnpm-lock.yaml pnpm-workspace.yaml tsconfig.json ./
COPY frontend/package.json frontend/
RUN corepack enable && pnpm install --frozen-lockfile
COPY frontend frontend
RUN corepack enable && pnpm install --frozen-lockfile
RUN cd frontend && pnpm build

FROM nginx:1.29-alpine3.22
RUN apk upgrade --no-cache
COPY ops/containers/web.conf /etc/nginx/conf.d/default.conf
COPY --from=build /app/frontend/dist /usr/share/nginx/html
EXPOSE 8080
USER nginx
ENTRYPOINT ["nginx"]
CMD ["-g", "daemon off; pid /tmp/nginx.pid; error_log /dev/stderr warn;"]
