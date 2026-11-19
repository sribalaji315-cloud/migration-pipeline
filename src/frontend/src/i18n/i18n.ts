import i18n from 'i18next'
import { initReactI18next } from 'react-i18next'
import common from './locales/en/common.json'
import pipeline from './locales/en/pipeline.json'
import issues from './locales/en/issues.json'
import errors from './locales/en/errors.json'
import help from './locales/en/help.json'

i18n.use(initReactI18next).init({
  lng: 'en',
  fallbackLng: 'en',
  ns: ['common', 'pipeline', 'issues', 'errors', 'help'],
  defaultNS: 'common',
  interpolation: { escapeValue: false },
  resources: {
    en: { common, pipeline, issues, errors, help },
  },
})

export default i18n
