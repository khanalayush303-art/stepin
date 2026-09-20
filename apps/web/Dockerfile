# syntax=docker/dockerfile:1

# ------------------------------------------------------------------ deps
FROM node:22-alpine AS deps
WORKDIR /app
COPY package.json package-lock.json ./
RUN npm ci

# ----------------------------------------------------------------- build
FROM node:22-alpine AS build
WORKDIR /app

# NEXT_PUBLIC_* values are inlined at build time, so this has to be a build arg
# rather than a runtime env var. API_INTERNAL_BASE_URL joins them for a
# different reason: next.config.ts's rewrites() destination is resolved once
# into .next's routes manifest at build time (output: "standalone" serves that
# static manifest — it never re-invokes next.config.ts at runtime), so a
# docker-compose `environment:` value alone arrives too late to affect it.
ARG NEXT_PUBLIC_API_BASE_URL=http://localhost:5080
ARG NEXT_PUBLIC_CLERK_PUBLISHABLE_KEY=
ARG API_INTERNAL_BASE_URL=http://localhost:5080
ENV NEXT_PUBLIC_API_BASE_URL=$NEXT_PUBLIC_API_BASE_URL \
    NEXT_PUBLIC_CLERK_PUBLISHABLE_KEY=$NEXT_PUBLIC_CLERK_PUBLISHABLE_KEY \
    API_INTERNAL_BASE_URL=$API_INTERNAL_BASE_URL \
    NEXT_TELEMETRY_DISABLED=1
COPY --from=deps /app/node_modules ./node_modules
COPY . .
RUN npm run build

# --------------------------------------------------------------- runtime
FROM node:22-alpine AS runtime
WORKDIR /app

ENV NODE_ENV=production \
    NEXT_TELEMETRY_DISABLED=1 \
    PORT=3000

RUN addgroup -g 1001 -S nodejs && adduser -S nextjs -u 1001

# `output: "standalone"` in next.config.ts produces this tree — it ships only the
# files the server actually needs, not the whole node_modules.
COPY --from=build --chown=nextjs:nodejs /app/.next/standalone ./
COPY --from=build --chown=nextjs:nodejs /app/.next/static ./.next/static
COPY --from=build --chown=nextjs:nodejs /app/public ./public

USER nextjs
EXPOSE 3000

CMD ["node", "server.js"]
