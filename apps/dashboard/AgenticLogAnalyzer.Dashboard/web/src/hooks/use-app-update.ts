import { useCallback, useEffect, useState } from "react"

import { getAppUpdate, type AppUpdateStatus } from "@/lib/api"

/** Polls the desktop auto-update status; stays null in the web dashboard. */
export function useAppUpdate() {
  const [status, setStatus] = useState<AppUpdateStatus | null>(null)

  const refresh = useCallback(async () => {
    try {
      setStatus(await getAppUpdate())
    } catch {
      // The API may be restarting; the next poll retries.
    }
  }, [])

  useEffect(() => {
    let active = true
    const poll = async () => {
      const next = await getAppUpdate().catch(() => undefined)
      if (!active || next === undefined) return
      setStatus(next)
    }
    void poll()
    const timer = window.setInterval(() => void poll(), 60_000)
    return () => {
      active = false
      window.clearInterval(timer)
    }
  }, [])

  return { status, setStatus, refresh }
}
