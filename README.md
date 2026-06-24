# Nacka Företagarträff - AI Matchmaking App 🚀

Detta är en modern webbapplikation skapad för att maximera nätverkandet under mässan Nacka Företagarträff. Appen använder en AI-mockup för att para ihop deltagare baserat på deras expertis, utmaningar och intressen.

**OBS:** Detta projekt har konverterats från Vanilla JS till **Angular**.

## ✨ Funktioner

### För Deltagare:
- **Snabb Onboarding:** Svara på 3 strategiska frågor om dina superkrafter och behov.
- **Profilhantering:** Skapa ett digitalt kontaktkort med bild, namn och företag (GDPR-säkrat).
- **Intelligenta Matchningar:** Se dina mest relevanta matchningar med procentsatser och motiveringar.
- **Interaktivt Nätverkande:** Skicka förfrågningar, chatta och boka möten direkt i appen.
- **Mötesfeedback:** Utvärdera dina möten på en skala 1-5 för att hjälpa AI:n bli ännu bättre.

### För Administratörer:
- **Dashboard:** Realtidsstatistik över registreringar och matchningskvalitet.
- **Import:** Enkel import av deltagare från befintliga bokningssystem.
- **Inbjudningssystem:** Skicka ut inbjudningar och påminnelser via e-post.
- **Resultatdistribution:** Publicera och skicka ut matchningsresultat med ett knapptryck.

## 🛠 Teknologi

- **Framework:** Angular 17+ (Standalone Components, Signals, Router).
- **Frontend:** HTML5, CSS3 (Glassmorphism-design portad till Angular).
- **State Management:** Angular Signals (in `MatchmakingService`).
- **Design:** Modern, mobilanpassad "Dark Mode" med fokus på användarvänlighet och estetik.

## 🚀 Kom igång

För att köra projektet lokalt:

1. Installera beroenden: `npm install`
2. Starta utvecklingsservern: `npm start`
3. Öppna `http://localhost:4200` i din webbläsare.

För att bygga för produktion: `npm run build`

## 🔒 GDPR & Integritet

Appen är designad med "Privacy by Design":
- Inga kontaktuppgifter delas automatiskt.
- Deltagare väljer själva när de vill dela information i personliga möten.
- All matchningsdata raderas efter mässans slut.

---
*Skapad med fokus på tillväxt och nätverkande i Nacka.* 📍✨
