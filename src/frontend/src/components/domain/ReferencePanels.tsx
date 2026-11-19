import { Alert, Button, FileInput, SimpleGrid, Stack } from '@mantine/core'
import type { ColDef } from 'ag-grid-community'
import { Upload } from 'lucide-react'
import { useState } from 'react'
import { useTranslation } from 'react-i18next'
import { formatDateTime } from '../../lib/format'
import { bluestarFileKinds, type BluestarFileKind, type ReferenceVersion } from '../../types/pipeline'
import { DataGrid } from '../ui/DataGrid'

type ReferenceUploadFormProps = {
  submitting: boolean
  errorMessage?: string
  onSubmit: (files: Record<BluestarFileKind, File>) => void
}

export function ReferenceUploadForm({ submitting, errorMessage, onSubmit }: ReferenceUploadFormProps) {
  const { t } = useTranslation('pipeline')
  const [files, setFiles] = useState<Partial<Record<BluestarFileKind, File | null>>>({})
  const complete = bluestarFileKinds.every((kind) => files[kind])

  return (
    <Stack>
      <SimpleGrid cols={{ base: 1, md: 2 }}>
        {bluestarFileKinds.map((kind) => (
          <FileInput
            key={kind}
            label={t(`reference.kinds.${kind}`)}
            description={t(`reference.fileNames.${kind}`)}
            accept=".xlsx"
            clearable
            value={files[kind] ?? null}
            onChange={(file) => setFiles((current) => ({ ...current, [kind]: file }))}
          />
        ))}
      </SimpleGrid>
      {errorMessage ? <Alert color="nordRed">{errorMessage}</Alert> : null}
      <Button
        leftSection={<Upload size={16} />}
        disabled={!complete}
        loading={submitting}
        onClick={() => onSubmit(files as Record<BluestarFileKind, File>)}
        w="fit-content"
      >
        {t('reference.upload')}
      </Button>
    </Stack>
  )
}

type ReferenceRow = { version: number; uploadedAt: string; table: string; rows: number; sha256: string }

export function ReferenceVersionTable({ versions, loading }: { versions: ReferenceVersion[]; loading: boolean }) {
  const { t } = useTranslation('pipeline')
  const rows: ReferenceRow[] = versions.flatMap((v) =>
    v.files.map((f) => ({ version: v.version, uploadedAt: v.uploadedAt, table: f.bronzeTable, rows: f.rows, sha256: f.sha256 })),
  )
  const columns: ColDef<ReferenceRow>[] = [
    { field: 'version', headerName: t('reference.version'), valueFormatter: (p) => `bs-${p.value}`, maxWidth: 120 },
    { field: 'uploadedAt', headerName: t('reference.uploadedAt'), valueFormatter: (p) => formatDateTime(p.value) },
    { field: 'table', headerName: t('reference.table') },
    { field: 'rows', headerName: t('reference.rows'), maxWidth: 140 },
    { field: 'sha256', headerName: t('reference.sha256'), minWidth: 260 },
  ]

  return (
    <DataGrid rows={rows} columns={columns} loading={loading} emptyMessage={t('reference.empty')} getRowId={(r) => `${r.version}|${r.table}`} />
  )
}
