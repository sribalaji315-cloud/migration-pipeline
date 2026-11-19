import { Alert, Stack } from '@mantine/core'
import { AgGridReact } from 'ag-grid-react'
import type { ColDef, RowClickedEvent } from 'ag-grid-community'

type DataGridProps<T> = {
  rows: T[]
  columns: ColDef<T>[]
  height?: number
  loading?: boolean
  emptyMessage?: string
  errorMessage?: string
  getRowId?: (row: T) => string
  onRowClick?: (row: T) => void
}

/** AG Grid wrapper. Paging is done server-side by the page; the grid only renders the current page. */
export function DataGrid<T>({ rows, columns, height = 320, loading = false, emptyMessage, errorMessage, getRowId, onRowClick }: DataGridProps<T>) {
  const handleRowClick = onRowClick
    ? (event: RowClickedEvent<T>) => {
        // Buttons/links inside a cell handle their own click; it must not also trigger the row action.
        const target = event.event?.target
        if (target instanceof Element && target.closest('button, a')) {
          return
        }

        if (event.data) {
          onRowClick(event.data)
        }
      }
    : undefined

  return (
    <Stack gap="xs">
      {errorMessage ? <Alert color="nordRed">{errorMessage}</Alert> : null}
      {!loading && !errorMessage && rows.length === 0 && emptyMessage ? <Alert color="nordBlue">{emptyMessage}</Alert> : null}
      <div className={`ag-theme-quartz data-grid${onRowClick ? ' data-grid-clickable' : ''}`} style={{ height }}>
        <AgGridReact<T>
          rowData={rows}
          columnDefs={columns}
          defaultColDef={{ flex: 1, minWidth: 110, sortable: true, filter: true, resizable: true }}
          getRowId={getRowId ? (params) => getRowId(params.data) : undefined}
          onRowClicked={handleRowClick}
          suppressCellFocus
          loading={loading}
          theme="legacy"
        />
      </div>
    </Stack>
  )
}
