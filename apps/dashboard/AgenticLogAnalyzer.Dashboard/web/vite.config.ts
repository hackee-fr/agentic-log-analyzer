import path from "node:path"
import { fileURLToPath } from "node:url"
import tailwindcss from "@tailwindcss/vite"
import react from "@vitejs/plugin-react"
import { defineConfig } from "vite"

const sourceDirectory = fileURLToPath(new URL("./src", import.meta.url))

export default defineConfig({
  plugins: [react(), tailwindcss()],
  server: {
    host: "localhost",
    port: 5173,
    strictPort: true,
  },
  optimizeDeps: {
    include: [
      "react",
      "react-dom/client",
      "lucide-react",
      "recharts",
      "sonner",
      "next-themes",
      "radix-ui",
      "class-variance-authority",
      "cn",
    ],
  },
  resolve: {
    alias: {
      "@": path.resolve(sourceDirectory),
    },
  },
})
