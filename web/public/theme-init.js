// Set the theme before first paint so there is no flash. "system" follows the OS setting.
// This is a separate file so the content security policy can refuse inline scripts.
try {
  var stored = localStorage.getItem('pulse-theme')
  var dark = stored === 'dark' || (stored !== 'light' && matchMedia('(prefers-color-scheme: dark)').matches)
  document.documentElement.dataset.theme = dark ? 'dark' : 'light'
} catch (e) {
  document.documentElement.dataset.theme = 'light'
}
