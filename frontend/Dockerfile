FROM node:22-alpine AS build
ARG APP
ARG VITE_API_URL
ARG VITE_STOREFRONT_URL
ENV VITE_API_URL=$VITE_API_URL
ENV VITE_STOREFRONT_URL=$VITE_STOREFRONT_URL
WORKDIR /app
COPY package.json ./
COPY packages/ ./packages/
COPY apps/ ./apps/
RUN npm install --legacy-peer-deps
RUN npm run build -w $APP

FROM nginx:alpine
ARG APP
COPY --from=build /app/apps/$APP/dist /usr/share/nginx/html
# An app with its own nginx.<app>.conf (the storefront) uses it; the rest share nginx.conf.
COPY nginx*.conf /tmp/nginx/
RUN cp "/tmp/nginx/nginx.$APP.conf" /etc/nginx/conf.d/default.conf 2>/dev/null \
    || cp /tmp/nginx/nginx.conf /etc/nginx/conf.d/default.conf; \
    rm -rf /tmp/nginx
EXPOSE 80
