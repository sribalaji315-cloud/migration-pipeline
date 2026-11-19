import { Group, SegmentedControl, TagsInput, TextInput } from '@mantine/core'
import { useTranslation } from 'react-i18next'
import type { RunOptions } from '../../lib/runOptions'
import type { BomTypePolicyMode } from '../../types/pipeline'

type RunOptionsFieldsProps = {
  value: RunOptions
  onChange: (value: RunOptions) => void
}

/** Effective date + BOM-type policy, pinned with every run. */
export function RunOptionsFields({ value, onChange }: RunOptionsFieldsProps) {
  const { t } = useTranslation('pipeline')

  return (
    <Group align="flex-end" wrap="wrap">
      <TextInput
        type="date"
        label={t('launcher.effectiveDate')}
        value={value.effectiveDate}
        onChange={(e) => onChange({ ...value, effectiveDate: e.currentTarget.value })}
      />
      <SegmentedControl
        value={value.mode}
        onChange={(mode) => onChange({ ...value, mode: mode as BomTypePolicyMode })}
        data={[
          { value: 'AllTypesUnfiltered', label: t('policy.allTypes') },
          { value: 'IncludeOnly', label: t('policy.includeOnlyShort') },
        ]}
      />
      {value.mode === 'IncludeOnly' ? (
        <TagsInput
          label={t('launcher.bomTypes')}
          placeholder={t('launcher.bomTypesPlaceholder')}
          value={value.types}
          onChange={(types) => onChange({ ...value, types })}
        />
      ) : null}
    </Group>
  )
}
