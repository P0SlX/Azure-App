# Affiche l'URL de la web app
output "url" {
    value = azurerm_linux_web_app.gaming_1.default_hostname
}