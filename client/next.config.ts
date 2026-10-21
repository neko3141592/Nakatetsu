import type { NextConfig } from "next";

const nextConfig: NextConfig = {
  /* config options here */
  cacheComponents: true,
  partialPrefetching: true,
  turbopack: {
    rules: {
      "*.css": {
        loaders: ["@tailwindcss/turbopack"],
        as: "*.css",
      },
    },
  },
  allowedDevOrigins: ['192.168.2.104', '172.16.80.254'],
  async rewrites() {
    return [{
      source: "/api/route-requests",
      destination: "http://127.0.0.1:8080/api/route-requests",
    }];
  },

};



export default nextConfig;
