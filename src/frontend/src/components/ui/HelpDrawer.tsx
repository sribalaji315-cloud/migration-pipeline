import { Button, Drawer, Stack, Text, Title } from '@mantine/core'
import { useDisclosure } from '@mantine/hooks'
import { HelpCircle } from 'lucide-react'

type HelpDrawerProps = {
  buttonLabel: string
  title: string
  sections: Array<{ heading: string; body: string }>
}

export function HelpDrawer({ buttonLabel, title, sections }: HelpDrawerProps) {
  const [opened, { open, close }] = useDisclosure(false)

  return (
    <>
      <Button variant="subtle" leftSection={<HelpCircle size={16} />} onClick={open}>{buttonLabel}</Button>
      <Drawer opened={opened} onClose={close} position="right" title={title} size="md">
        <Stack>
          {sections.map((section) => (
            <Stack gap={4} key={section.heading}>
              <Title order={5}>{section.heading}</Title>
              <Text size="sm">{section.body}</Text>
            </Stack>
          ))}
        </Stack>
      </Drawer>
    </>
  )
}
