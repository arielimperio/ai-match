/** @type {import('tailwindcss').Config} */
module.exports = {
  content: [
    "./src/**/*.{html,ts}",
  ],
  theme: {
    extend: {
      colors: {
        primary: 'var(--primary)',
        secondary: 'var(--secondary)',
        'bg-dark': 'var(--bg-dark)',
        'card-bg': 'var(--card-bg)',
        'card-border': 'var(--card-border)',
        'text-main': 'var(--text-main)',
        'text-muted': 'var(--text-muted)',
        green: 'var(--green)',
        yellow: 'var(--yellow)',
        accent: 'var(--accent)',
      },
      backgroundColor: {
        primary: 'var(--primary)',
        secondary: 'var(--secondary)',
        'bg-dark': 'var(--bg-dark)',
        'card-bg': 'var(--card-bg)',
      },
      textColor: {
        primary: 'var(--primary)',
        secondary: 'var(--secondary)',
        'text-main': 'var(--text-main)',
        'text-muted': 'var(--text-muted)',
        green: 'var(--green)',
        yellow: 'var(--yellow)',
      },
      borderColor: {
        primary: 'var(--primary)',
        secondary: 'var(--secondary)',
        'card-border': 'var(--card-border)',
      },
      boxShadow: {
        'primary-glow': '0 0 20px var(--primary-glow)',
      },
      backdropBlur: {
        glass: '12px',
      },
    },
  },
  plugins: [],
}
