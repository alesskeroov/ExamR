import i18n from 'i18next'
import LanguageDetector from 'i18next-browser-languagedetector'
import { initReactI18next } from 'react-i18next'
import az from '../locales/az.json'
import en from '../locales/en.json'
import ru from '../locales/ru.json'

export const languages = [
  { code: 'az', label: 'AZ', name: 'Azərbaycan dili' },
  { code: 'en', label: 'EN', name: 'English' },
  { code: 'ru', label: 'RU', name: 'Русский' },
] as const

export type Language = (typeof languages)[number]['code']

const storageKey = 'examr-language'

// Azerbaijani is the default: only an explicit choice (saved in localStorage) switches the interface language.
void i18n
  .use(LanguageDetector)
  .use(initReactI18next)
  .init({
    resources: { az: { translation: az }, en: { translation: en }, ru: { translation: ru } },
    supportedLngs: languages.map(l => l.code),
    fallbackLng: 'az',
    nonExplicitSupportedLngs: true,
    load: 'languageOnly',
    detection: { order: ['localStorage'], lookupLocalStorage: storageKey, caches: ['localStorage'] },
    interpolation: { escapeValue: false },
  })

const syncDocumentLanguage = (lng: string) => { document.documentElement.lang = currentLanguage(lng) }
syncDocumentLanguage(i18n.language)
i18n.on('languageChanged', syncDocumentLanguage)

/** The active interface language, always one of the supported codes. */
export function currentLanguage(lng = i18n.resolvedLanguage ?? i18n.language): Language {
  return languages.find(l => lng?.startsWith(l.code))?.code ?? 'az'
}

/** BCP 47 tag for Intl/Date formatting in the active language. */
export function localeTag(): string {
  return { az: 'az-AZ', en: 'en-GB', ru: 'ru-RU' }[currentLanguage()]
}

export default i18n
