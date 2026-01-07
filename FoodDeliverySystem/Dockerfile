# Build stage
FROM node:18-alpine AS build

WORKDIR /app

# Копируем package.json и package-lock.json
COPY package.json ./
COPY package-lock.json ./

# Устанавливаем зависимости
RUN npm install --omit=dev

# Копируем весь исходный код
COPY . .

# Собираем приложение
RUN npm run build

# Production stage
FROM nginx:alpine

# Копируем собранное приложение в nginx
COPY --from=build /app/build /usr/share/nginx/html

# Копируем конфигурацию nginx
COPY nginx.conf /etc/nginx/conf.d/default.conf

EXPOSE 80

CMD ["nginx", "-g", "daemon off;"]