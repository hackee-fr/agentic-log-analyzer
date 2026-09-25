import type { CanonicalEvent } from "@/lib/api"

export type ActivityBucket = {
  label: string
  start: number
  events: number
  failures: number
  warnings: number
}

const second = 1000
const minute = 60 * second
const hour = 60 * minute
const day = 24 * hour
const steps = [second, 5 * second, 10 * second, 30 * second, minute, 5 * minute, 15 * minute, 30 * minute, hour, 3 * hour, 6 * hour, 12 * hour, day]

const isFailure = (result: string | null) => /fail|error/i.test(result ?? "")
const isWarning = (result: string | null) => /warn/i.test(result ?? "")

/** Groups events into at most `maxBuckets` equal time slots, picking the step from the span of the data. */
export function buildActivity(events: CanonicalEvent[], maxBuckets = 30): { buckets: ActivityBucket[]; step: number } {
  if (!events.length) return { buckets: [], step: minute }

  const times = events.map((event) => new Date(event.timestamp).getTime())
  const min = Math.min(...times)
  const max = Math.max(...times)
  const step = steps.find((candidate) => (max - min) / candidate < maxBuckets) ?? day
  const first = Math.floor(min / step) * step
  const count = Math.floor((max - first) / step) + 1

  const format = new Intl.DateTimeFormat("en-GB", step < minute
    ? { hour: "2-digit", minute: "2-digit", second: "2-digit" }
    : step < day
      ? { hour: "2-digit", minute: "2-digit" }
      : { day: "2-digit", month: "short" })

  const buckets: ActivityBucket[] = Array.from({ length: count }, (_, index) => {
    const start = first + index * step
    return { label: format.format(start), start, events: 0, failures: 0, warnings: 0 }
  })

  events.forEach((event, index) => {
    const bucket = buckets[Math.floor((times[index] - first) / step)]
    bucket.events += 1
    if (isFailure(event.result)) bucket.failures += 1
    else if (isWarning(event.result)) bucket.warnings += 1
  })

  return { buckets, step }
}

export function describeStep(step: number) {
  if (step < minute) return `${step / second}s`
  if (step < hour) return `${step / minute} min`
  if (step < day) return `${step / hour} h`
  return "1 day"
}
