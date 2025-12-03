# watch-tailwind.ps1
# Shortcut script to build TailwindCSS in watch mode
# Run from inside the "tools" folder

# Force execution bypass just for this process
powershell -ExecutionPolicy Bypass `
    npx tailwindcss `
    -i ../RegistrationSystem.Web/wwwroot/tailwindcss/input.css `
    -o ../RegistrationSystem.Web/wwwroot/tailwindcss/output.css `
    --watch
