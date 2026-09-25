import { useState } from "react"
import { Download, LoaderCircle, RefreshCw, RotateCw } from "lucide-react"
import { toast } from "sonner"

import { applyAppUpdate, checkAppUpdate, type AppUpdateStatus } from "@/lib/api"
import { Badge } from "@/components/ui/badge"
import { Button } from "@/components/ui/button"

async function restartToUpdate() {
  try {
    await applyAppUpdate()
    toast.success("Installing the update…", { description: "The app restarts on the new version in a few seconds." })
  } catch (error) {
    toast.error("Update failed", { description: error instanceof Error ? error.message : "Try again later." })
  }
}

export function AppUpdateBanner({ status }: { status: AppUpdateStatus | null }) {
  const [applying, setApplying] = useState(false)
  if (status?.state !== "ready") return null
  return (
    <div role="status" className="mb-6 flex flex-col gap-3 rounded-xl border border-blue-500/30 bg-blue-500/10 px-4 py-3 text-sm text-blue-100 sm:flex-row sm:items-center">
      <Download className="size-4 shrink-0 text-blue-400" />
      <span className="flex-1">
        Version <strong className="font-semibold text-foreground">{status.availableVersion}</strong> is ready. Restart to update, or it installs when you close the app.
      </span>
      <Button size="sm" disabled={applying} onClick={() => { setApplying(true); void restartToUpdate() }} className="h-8 gap-1.5 bg-primary text-xs text-primary-foreground hover:bg-primary/90">
        {applying ? <LoaderCircle className="size-3.5 animate-spin" /> : <RotateCw className="size-3.5" />}
        Restart now
      </Button>
    </div>
  )
}

const stateLabel: Record<AppUpdateStatus["state"], [string, string]> = {
  disabled: ["Not installed", "border-border bg-muted/40 text-foreground/70"],
  idle: ["Waiting", "border-border bg-muted/40 text-foreground/70"],
  checking: ["Checking", "border-blue-500/30 bg-blue-500/10 text-blue-400"],
  "up-to-date": ["Up to date", "border-emerald-500/30 bg-emerald-500/10 text-emerald-400"],
  downloading: ["Downloading", "border-blue-500/30 bg-blue-500/10 text-blue-400"],
  ready: ["Ready to install", "border-blue-500/30 bg-blue-500/10 text-blue-400"],
  error: ["Check failed", "border-amber-500/30 bg-amber-500/10 text-amber-400"],
}

/** Settings row: current version, update state and a manual check. Hidden in the web dashboard. */
export function AppUpdateSettings({ status, onChange }: { status: AppUpdateStatus | null; onChange: (status: AppUpdateStatus) => void }) {
  const [checking, setChecking] = useState(false)
  if (!status) return null
  const [label, tone] = stateLabel[status.state]

  const check = async () => {
    setChecking(true)
    try {
      onChange(await checkAppUpdate())
    } catch (error) {
      toast.error("Update check failed", { description: error instanceof Error ? error.message : "Try again later." })
    } finally {
      setChecking(false)
    }
  }

  return (
    <div className="space-y-2 rounded-lg border border-border px-3 py-2.5 text-[11px] text-muted-foreground">
      <div className="flex items-center justify-between gap-3">
        <span className="text-xs text-foreground/70">Application updates</span>
        <Badge variant="outline" className={tone}>{status.state === "downloading" ? `${label} ${status.progress}%` : label}</Badge>
      </div>
      <div className="flex justify-between gap-3"><span>Installed version</span><span className="font-mono text-foreground/80">{status.currentVersion ?? "Development build"}</span></div>
      {status.availableVersion && <div className="flex justify-between gap-3"><span>Available version</span><span className="font-mono text-foreground/80">{status.availableVersion}</span></div>}
      {status.error && <p className="text-amber-300">{status.error}</p>}
      {status.installed && (
        <div className="flex justify-end gap-2 pt-1">
          {status.state === "ready" && <Button size="sm" onClick={() => void restartToUpdate()} className="h-7 gap-1.5 text-[11px]"><RotateCw className="size-3" />Restart now</Button>}
          <Button variant="outline" size="sm" disabled={checking || status.state === "checking" || status.state === "downloading"} onClick={() => void check()} className="h-7 gap-1.5 text-[11px]">
            <RefreshCw className={`size-3 ${checking ? "animate-spin" : ""}`} />Check now
          </Button>
        </div>
      )}
    </div>
  )
}
