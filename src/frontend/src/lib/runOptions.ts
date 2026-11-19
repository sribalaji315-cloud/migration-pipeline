import { todayIso } from './format'
import type { BomTypePolicyMode, RunRequest } from '../types/pipeline'

export type RunOptions = {
  effectiveDate: string
  mode: BomTypePolicyMode
  types: string[]
}

export function defaultRunOptions(): RunOptions {
  return { effectiveDate: todayIso(), mode: 'AllTypesUnfiltered', types: [] }
}

export function isRunOptionsValid(options: RunOptions) {
  return options.mode === 'AllTypesUnfiltered' || options.types.length > 0
}

export function toRunRequest(options: RunOptions): RunRequest {
  return {
    effectiveDate: options.effectiveDate || null,
    bomTypePolicy: { mode: options.mode, types: options.mode === 'IncludeOnly' ? options.types : null },
  }
}
