using System;
using System.Collections.Generic;
using System.Globalization;
using System.Text.RegularExpressions;

namespace PrintPilotProxy.App.Localization;

/// <summary>
/// Translates known system log messages, activity status errors, proxy states,
/// and service states into the active UI language across 13 supported languages.
/// </summary>
public static class LogLocalizer
{
    private sealed record Rule(Regex Pattern, Dictionary<string, string> Templates);

    private static readonly List<Rule> Rules = new();

    static LogLocalizer()
    {
        // ─── Status and States ───────────────────────────────────────────────
        AddRule(@"^Running$", new()
        {
            ["tr"] = "Çalışıyor",
            ["de"] = "Wird ausgeführt",
            ["fr"] = "En cours d'exécution",
            ["es"] = "En ejecución",
            ["pt"] = "Em execução",
            ["it"] = "In esecuzione",
            ["nl"] = "Actief",
            ["pl"] = "Uruchomiono",
            ["ro"] = "Rulează",
            ["bg"] = "Работи",
            ["cs"] = "Spuštěno",
            ["sv"] = "Körs"
        });

        AddRule(@"^Stopped$", new()
        {
            ["tr"] = "Durduruldu",
            ["de"] = "Angehalten",
            ["fr"] = "Arrêté",
            ["es"] = "Detenido",
            ["pt"] = "Parado",
            ["it"] = "Arrestato",
            ["nl"] = "Gestopt",
            ["pl"] = "Zatrzymano",
            ["ro"] = "Oprit",
            ["bg"] = "Спряно",
            ["cs"] = "Zastaveno",
            ["sv"] = "Stoppad"
        });

        AddRule(@"^Starting$", new()
        {
            ["tr"] = "Başlatılıyor",
            ["de"] = "Wird gestartet",
            ["fr"] = "Démarrage",
            ["es"] = "Iniciando",
            ["pt"] = "Iniciando",
            ["it"] = "Avvio in corso",
            ["nl"] = "Starten",
            ["pl"] = "Uruchamianie",
            ["ro"] = "Se pornește",
            ["bg"] = "Стартира се",
            ["cs"] = "Spouští se",
            ["sv"] = "Startar"
        });

        AddRule(@"^Stopping$", new()
        {
            ["tr"] = "Durduruluyor",
            ["de"] = "Wird angehalten",
            ["fr"] = "Arrêt en cours",
            ["es"] = "Deteniendo",
            ["pt"] = "Parando",
            ["it"] = "Arresto in corso",
            ["nl"] = "Stoppen",
            ["pl"] = "Zatrzymywanie",
            ["ro"] = "Se oprește",
            ["bg"] = "Спира се",
            ["cs"] = "Zastavuje se",
            ["sv"] = "Stoppar"
        });

        AddRule(@"^Faulted$", new()
        {
            ["tr"] = "Hatalı",
            ["de"] = "Fehlerhaft",
            ["fr"] = "En erreur",
            ["es"] = "Con errores",
            ["pt"] = "Com falha",
            ["it"] = "In errore",
            ["nl"] = "Foutstatus",
            ["pl"] = "Błąd",
            ["ro"] = "Defect",
            ["bg"] = "Грешка",
            ["cs"] = "Chyba",
            ["sv"] = "Felaktig"
        });

        AddRule(@"^NotInstalled$", new()
        {
            ["tr"] = "Kurulu Değil",
            ["de"] = "Nicht installiert",
            ["fr"] = "Non installé",
            ["es"] = "No instalado",
            ["pt"] = "Não instalado",
            ["it"] = "Non installato",
            ["nl"] = "Niet geïnstalleerd",
            ["pl"] = "Nie zainstalowano",
            ["ro"] = "Neinstalat",
            ["bg"] = "Не е инсталиран",
            ["cs"] = "Není nainstalováno",
            ["sv"] = "Inte installerad"
        });

        AddRule(@"^Automatic$", new()
        {
            ["tr"] = "Otomatik",
            ["de"] = "Automatisch",
            ["fr"] = "Automatique",
            ["es"] = "Automático",
            ["pt"] = "Automático",
            ["it"] = "Automatico",
            ["nl"] = "Automatisch",
            ["pl"] = "Automatyczny",
            ["ro"] = "Automat",
            ["bg"] = "Автоматично",
            ["cs"] = "Automaticky",
            ["sv"] = "Automatisk"
        });

        AddRule(@"^AutomaticDelayed$", new()
        {
            ["tr"] = "Otomatik (Gecikmeli)",
            ["de"] = "Automatisch (Verzögert)",
            ["fr"] = "Automatique (différé)",
            ["es"] = "Automático (inicio retrasado)",
            ["pt"] = "Automático (Atrasado)",
            ["it"] = "Automatico (avvio ritardato)",
            ["nl"] = "Automatisch (vertraagd)",
            ["pl"] = "Automatyczny (opóźniony)",
            ["ro"] = "Automat (întârziat)",
            ["bg"] = "Автоматично (отложено)",
            ["cs"] = "Automaticky (zpožděné)",
            ["sv"] = "Automatisk (fördröjd)"
        });

        AddRule(@"^Manual$", new()
        {
            ["tr"] = "Elle (Manuel)",
            ["de"] = "Manuell",
            ["fr"] = "Manuel",
            ["es"] = "Manual",
            ["pt"] = "Manual",
            ["it"] = "Manuale",
            ["nl"] = "Handmatig",
            ["pl"] = "Ręczny",
            ["ro"] = "Manual",
            ["bg"] = "Ръчно",
            ["cs"] = "Ručně",
            ["sv"] = "Manuell"
        });

        AddRule(@"^Disabled$", new()
        {
            ["tr"] = "Devre Dışı",
            ["de"] = "Deaktiviert",
            ["fr"] = "Désactivé",
            ["es"] = "Deshabilitado",
            ["pt"] = "Desativado",
            ["it"] = "Disabilitato",
            ["nl"] = "Uitgeschakeld",
            ["pl"] = "Wyłączony",
            ["ro"] = "Dezactivat",
            ["bg"] = "Деактивирано",
            ["cs"] = "Zakázáno",
            ["sv"] = "Inaktiverad"
        });

        // ─── Activity & Connection Errors ─────────────────────────────────────
        AddRule(@"^Proxy authentication required:\s*Missing proxy authorization header\.?$", new()
        {
            ["tr"] = "Proxy kimlik doğrulaması gerekiyor: Proxy yetkilendirme başlığı eksik.",
            ["de"] = "Proxy-Authentifizierung erforderlich: Fehlender Proxy-Autorisierungs-Header.",
            ["fr"] = "Authentification proxy requise : En-tête d'autorisation proxy manquant.",
            ["es"] = "Autenticación de proxy requerida: Falta el encabezado de autorización de proxy.",
            ["pt"] = "Autenticação proxy necessária: Cabeçalho de autorização do proxy ausente.",
            ["it"] = "Autenticazione proxy richiesta: Intestazione di autorizzazione proxy mancante.",
            ["nl"] = "Proxy-authenticatie vereist: Ontbrekende proxy-autorisatieheader.",
            ["pl"] = "Wymagane uwierzytelnienie proxy: Brak nagłówka autoryzacji proxy.",
            ["ro"] = "Autentificare proxy necesară: Lipsește antetul de autorizare proxy.",
            ["bg"] = "Изисква се прокси удостоверяване: Липсва заглавка за авторизация на прокси.",
            ["cs"] = "Je vyžadováno ověření proxy: Chybí autorizační hlavička proxy.",
            ["sv"] = "Proxy-autentisering krävs: Saknar proxy-auktoriseringsrubrik."
        });

        AddRule(@"^Proxy authentication required:\s*(.*)$", new()
        {
            ["tr"] = "Proxy kimlik doğrulaması gerekiyor: {0}",
            ["de"] = "Proxy-Authentifizierung erforderlich: {0}",
            ["fr"] = "Authentification proxy requise : {0}",
            ["es"] = "Autenticación de proxy requerida: {0}",
            ["pt"] = "Autenticação proxy necessária: {0}",
            ["it"] = "Autenticazione proxy richiesta: {0}",
            ["nl"] = "Proxy-authenticatie vereist: {0}",
            ["pl"] = "Wymagane uwierzytelnienie proxy: {0}",
            ["ro"] = "Autentificare proxy necesară: {0}",
            ["bg"] = "Изисква се прокси удостоверяване: {0}",
            ["cs"] = "Je vyžadováno ověření proxy: {0}",
            ["sv"] = "Proxy-autentisering krävs: {0}"
        });

        AddRule(@"^Missing proxy authorization header\.?$", new()
        {
            ["tr"] = "Proxy yetkilendirme başlığı eksik.",
            ["de"] = "Fehlender Proxy-Autorisierungs-Header.",
            ["fr"] = "En-tête d'autorisation proxy manquant.",
            ["es"] = "Falta el encabezado de autorización de proxy.",
            ["pt"] = "Cabeçalho de autorização do proxy ausente.",
            ["it"] = "Intestazione di autorizzazione proxy mancante.",
            ["nl"] = "Ontbrekende proxy-autorisatieheader.",
            ["pl"] = "Brak nagłówka autoryzacji proxy.",
            ["ro"] = "Lipsește antetul de autorizare proxy.",
            ["bg"] = "Липсва заглавка за авторизация на прокси.",
            ["cs"] = "Chybí autorizační hlavička proxy.",
            ["sv"] = "Saknar proxy-auktoriseringsrubrik."
        });

        AddRule(@"^Access denied by ACL\.?$", new()
        {
            ["tr"] = "Erişim listesi (ACL) tarafından engellendi.",
            ["de"] = "Zugriff durch ACL verweigert.",
            ["fr"] = "Accès refusé par la liste de contrôle d'accès (ACL).",
            ["es"] = "Acceso denegado por ACL.",
            ["pt"] = "Acesso negado pela ACL.",
            ["it"] = "Accesso negato da ACL.",
            ["nl"] = "Toegang geweigerd door ACL.",
            ["pl"] = "Dostęp zabroniony przez ACL.",
            ["ro"] = "Acces refuzat de ACL.",
            ["bg"] = "Достъпът е отказан от ACL.",
            ["cs"] = "Přístup byl odepřen seznamem ACL.",
            ["sv"] = "Åtkomst nekad av ACL."
        });

        AddRule(@"^Destination URI is missing\.?$", new()
        {
            ["tr"] = "Hedef URI adresi eksik.",
            ["de"] = "Ziel-URI fehlt.",
            ["fr"] = "L'URI de destination est manquant.",
            ["es"] = "Falta la URI de destino.",
            ["pt"] = "URI de destino ausente.",
            ["it"] = "URI di destinazione mancante.",
            ["nl"] = "Doel-URI ontbreekt.",
            ["pl"] = "Brak docelowego identyfikatora URI.",
            ["ro"] = "Lipsește URI-ul de destinație.",
            ["bg"] = "Липсва целевият URI адрес.",
            ["cs"] = "Chybí cílový identifikátor URI.",
            ["sv"] = "Mål-URI saknas."
        });

        AddRule(@"^Destination port (\d+) is not allowed\.?$", new()
        {
            ["tr"] = "Hedef port {0} için izin verilmiyor.",
            ["de"] = "Zielport {0} ist nicht erlaubt.",
            ["fr"] = "Le port de destination {0} n'est pas autorisé.",
            ["es"] = "El puerto de destino {0} no está permitido.",
            ["pt"] = "A porta de destino {0} não é permitida.",
            ["it"] = "La porta di destinazione {0} non è consentita.",
            ["nl"] = "Doelpoort {0} is niet toegestaan.",
            ["pl"] = "Port docelowy {0} nie jest dozwolony.",
            ["ro"] = "Portul de destinație {0} nu este permis.",
            ["bg"] = "Целевият порт {0} не е разрешен.",
            ["cs"] = "Cílový port {0} není povolen.",
            ["sv"] = "Målport {0} är inte tillåten."
        });

        AddRule(@"^Unsupported authorization scheme\. Expected (.*)\.?$", new()
        {
            ["tr"] = "Desteklenmeyen yetkilendirme şeması. Beklenen: {0}",
            ["de"] = "Nicht unterstütztes Autorisierungsschema. Erwartet: {0}",
            ["fr"] = "Schéma d'autorisation non pris en charge. Attendu : {0}",
            ["es"] = "Esquema de autorización no compatible. Esperado: {0}",
            ["pt"] = "Esquema de autorização não suportado. Esperado: {0}",
            ["it"] = "Schema di autorizzazione non supportato. Previsto: {0}",
            ["nl"] = "Niet-ondersteund autorisatieschema. Verwacht: {0}",
            ["pl"] = "Nieobsługiwany schemat autoryzacji. Oczekiwano: {0}",
            ["ro"] = "Schemă de autorizare neacceptată. Se aștepta: {0}",
            ["bg"] = "Неподдържана схема за авторизация. Очаква се: {0}",
            ["cs"] = "Nepodporované schéma autorizace. Očekáváno: {0}",
            ["sv"] = "Auktoriseringsschemat stöds inte. Förväntat: {0}"
        });

        AddRule(@"^Missing or invalid protocol version in authorization header\.?$", new()
        {
            ["tr"] = "Yetkilendirme başlığında protokol sürümü eksik veya geçersiz.",
            ["de"] = "Fehlende oder ungültige Protokollversion im Autorisierungs-Header.",
            ["fr"] = "Version de protocole manquante ou non valide dans l'en-tête d'autorisation.",
            ["es"] = "Versión de protocolo faltante o no válida en el encabezado de autorización.",
            ["pt"] = "Versão de protocolo ausente ou inválida no cabeçalho de autorização.",
            ["it"] = "Versione del protocollo mancante o non valida nell'intestazione di autorizzazione.",
            ["nl"] = "Ontbrekende of ongeldige protocolversie in autorisatieheader.",
            ["pl"] = "Brakująca lub nieprawidłowa wersja protokołu w nagłówku autoryzacji.",
            ["ro"] = "Versiune de protocol lipsă sau nevalidă în antetul de autorizare.",
            ["bg"] = "Липсваща или невалидна версия на протокола в заглавката за авторизация.",
            ["cs"] = "Chybějící nebo neplatná verze protokolu v autorizační hlavičce.",
            ["sv"] = "Protokollversion saknas eller är ogiltig i auktoriseringsrubriken."
        });

        AddRule(@"^Unsupported protocol version:\s*(\S+)\.\s*Expected\s*(\S+)\.?$", new()
        {
            ["tr"] = "Desteklenmeyen protokol sürümü: {0}. Beklenen: {1}",
            ["de"] = "Nicht unterstützte Protokollversion: {0}. Erwartet: {1}",
            ["fr"] = "Version de protocole non prise en charge : {0}. Attendu : {1}",
            ["es"] = "Versión de protocolo no compatible: {0}. Esperado: {1}",
            ["pt"] = "Versão de protocolo não suportada: {0}. Esperado: {1}",
            ["it"] = "Versione del protocollo non supportata: {0}. Previsto: {1}",
            ["nl"] = "Niet-ondersteunde protocolversie: {0}. Verwacht: {1}",
            ["pl"] = "Nieobsługiwana wersja protokołu: {0}. Oczekiwano: {1}",
            ["ro"] = "Versiune de protocol neacceptată: {0}. Se aștepta: {1}",
            ["bg"] = "Неподдържана версия на протокола: {0}. Очаква се: {1}",
            ["cs"] = "Nepodporovaná verze protokolu: {0}. Očekáváno: {1}",
            ["sv"] = "Protokollversion stöds inte: {0}. Förväntat: {1}"
        });

        AddRule(@"^Missing or invalid timestamp in authorization header\.?$", new()
        {
            ["tr"] = "Yetkilendirme başlığında zaman damgası eksik veya geçersiz.",
            ["de"] = "Fehlender oder ungültiger Zeitstempel im Autorisierungs-Header.",
            ["fr"] = "Horodatage manquant ou non valide dans l'en-tête d'autorisation.",
            ["es"] = "Marca de tiempo faltante o no válida en el encabezado de autorización.",
            ["pt"] = "Carimbo de data/hora ausente ou inválido no cabeçalho de autorização.",
            ["it"] = "Timestamp mancante o non valido nell'intestazione di autorizzazione.",
            ["nl"] = "Ontbrekende of ongeldige tijdstempel in autorisatieheader.",
            ["pl"] = "Brakujący lub nieprawidłowy znacznik czasu w nagłówku autoryzacji.",
            ["ro"] = "Marcaj temporal lipsă sau nevalid în antetul de autorizare.",
            ["bg"] = "Липсващо или невалидно клеймо за време в заглавката за авторизация.",
            ["cs"] = "Chybějící nebo neplatné časové razítko v autorizační hlavičce.",
            ["sv"] = "Tidsstämpel saknas eller är ogiltig i auktoriseringsrubriken."
        });

        AddRule(@"^Authorization timestamp expired or clock skew too large\.?$", new()
        {
            ["tr"] = "Yetkilendirme zaman damgası süresi doldu veya saat farkı çok büyük.",
            ["de"] = "Autorisierungs-Zeitstempel abgelaufen oder Zeitabweichung zu groß.",
            ["fr"] = "Horodatage d'autorisation expiré ou décalage d'horloge trop important.",
            ["es"] = "Marca de tiempo de autorización expirada o desfase de reloj demasiado grande.",
            ["pt"] = "Carimbo de data/hora de autorização expirado ou desvio de relógio muito grande.",
            ["it"] = "Timestamp di autorizzazione scaduto o differenza oraria eccessiva.",
            ["nl"] = "Autorisatietijdstempel verlopen of tijdsverschil te groot.",
            ["pl"] = "Znacznik czasu autoryzacji wygasł lub różnica zegara jest zbyt duża.",
            ["ro"] = "Marcajul temporal de autorizare a expirat sau diferența de ceas este prea mare.",
            ["bg"] = "Клеймото за време за авторизация е изтекло или отклонението на часовника е твърде голямо.",
            ["cs"] = "Platnost časového razítka autorizace vypršela nebo je odchylka hodin příliš velká.",
            ["sv"] = "Auktoriseringstidsstämpeln har gått ut eller klockavvikelsen är för stor."
        });

        AddRule(@"^Missing or invalid nonce in authorization header\.?$", new()
        {
            ["tr"] = "Yetkilendirme başlığında belirteç (nonce) eksik veya geçersiz.",
            ["de"] = "Fehlende oder ungültige Nonce im Autorisierungs-Header.",
            ["fr"] = "Nonce manquant ou non valide dans l'en-tête d'autorisation.",
            ["es"] = "Falta o no es válido el nonce en el encabezado de autorización.",
            ["pt"] = "Nonce ausente ou inválido no cabeçalho de autorização.",
            ["it"] = "Nonce mancante o non valido nell'intestazione di autorizzazione.",
            ["nl"] = "Ontbrekende of ongeldige nonce in autorisatieheader.",
            ["pl"] = "Brakujący lub nieprawidłowy nonce w nagłówku autoryzacji.",
            ["ro"] = "Nonce lipsă sau nevalid în antetul de autorizare.",
            ["bg"] = "Липсващ или невалиден nonce в заглавката за авторизация.",
            ["cs"] = "Chybějící nebo neplatný nonce v autorizační hlavičce.",
            ["sv"] = "Nonce saknas eller är ogiltig i auktoriseringsrubriken."
        });

        AddRule(@"^Missing signature in authorization header\.?$", new()
        {
            ["tr"] = "Yetkilendirme başlığında imza eksik.",
            ["de"] = "Fehlende Signatur im Autorisierungs-Header.",
            ["fr"] = "Signature manquante dans l'en-tête d'autorisation.",
            ["es"] = "Falta la firma en el encabezado de autorización.",
            ["pt"] = "Assinatura ausente no cabeçalho de autorização.",
            ["it"] = "Firma mancante nell'intestazione di autorizzazione.",
            ["nl"] = "Ontbrekende handtekening in autorisatieheader.",
            ["pl"] = "Brak podpisu w nagłówku autoryzacji.",
            ["ro"] = "Lipsește semnătura în antetul de autorizare.",
            ["bg"] = "Липсва подпис в заглавката за авторизация.",
            ["cs"] = "Chybí podpis v autorizační hlavičce.",
            ["sv"] = "Signatur saknas i auktoriseringsrubriken."
        });

        AddRule(@"^Invalid authorization signature\.?$", new()
        {
            ["tr"] = "Geçersiz yetkilendirme imzası.",
            ["de"] = "Ungültige Autorisierungssignatur.",
            ["fr"] = "Signature d'autorisation non valide.",
            ["es"] = "Firma de autorización no válida.",
            ["pt"] = "Assinatura de autorização inválida.",
            ["it"] = "Firma di autorizzazione non valida.",
            ["nl"] = "Ongeldige autorisatiehandtekening.",
            ["pl"] = "Nieprawidłowy podpis autoryzacji.",
            ["ro"] = "Semnătură de autorizare nevalidă.",
            ["bg"] = "Невалиден подпис за авторизация.",
            ["cs"] = "Neplatný podpis autorizace.",
            ["sv"] = "Ogiltig auktoriseringssignatur."
        });

        // ─── System Logs ──────────────────────────────────────────────────────
        AddRule(@"^Configuration saved successfully\.?$", new()
        {
            ["tr"] = "Yapılandırma başarıyla kaydedildi.",
            ["de"] = "Konfiguration erfolgreich gespeichert.",
            ["fr"] = "Configuration enregistrée avec succès.",
            ["es"] = "Configuración guardada exitosamente.",
            ["pt"] = "Configuração salva com sucesso.",
            ["it"] = "Configurazione salvata con successo.",
            ["nl"] = "Configuratie succesvol opgeslagen.",
            ["pl"] = "Konfiguracja została pomyślnie zapisana.",
            ["ro"] = "Configurația a fost salvată cu succes.",
            ["bg"] = "Конфигурацията е запазена успешно.",
            ["cs"] = "Konfigurace byla úspěšně uložena.",
            ["sv"] = "Konfigurationen har sparats."
        });

        AddRule(@"^Configuration backed up to (.*)$", new()
        {
            ["tr"] = "Yapılandırma şuraya yedeklendi: {0}",
            ["de"] = "Konfiguration gesichert unter: {0}",
            ["fr"] = "Configuration sauvegardée vers : {0}",
            ["es"] = "Copia de seguridad de la configuración creada en: {0}",
            ["pt"] = "Backup da configuração salvo em: {0}",
            ["it"] = "Backup della configurazione creato in: {0}",
            ["nl"] = "Back-up van configuratie opgeslagen in: {0}",
            ["pl"] = "Kopia zapasowa konfiguracji w: {0}",
            ["ro"] = "Copie de rezervă a configurației salvată în: {0}",
            ["bg"] = "Архивът на конфигурацията е записан в: {0}",
            ["cs"] = "Záloha konfigurace vytvořena v: {0}",
            ["sv"] = "Konfigurationssäkerhetskopia sparad till: {0}"
        });

        AddRule(@"^Configuration restored successfully from (.*)$", new()
        {
            ["tr"] = "Yapılandırma şuradan başarıyla geri yüklendi: {0}",
            ["de"] = "Konfiguration erfolgreich wiederhergestellt von: {0}",
            ["fr"] = "Configuration restaurée avec succès depuis : {0}",
            ["es"] = "Configuración restaurada con éxito desde: {0}",
            ["pt"] = "Configuração restaurada com sucesso de: {0}",
            ["it"] = "Configurazione ripristinata con successo da: {0}",
            ["nl"] = "Configuratie succesvol hersteld van: {0}",
            ["pl"] = "Konfiguracja pomyślnie przywrócona z: {0}",
            ["ro"] = "Configurație restaurată cu succes din: {0}",
            ["bg"] = "Конфигурацията е възстановена успешно от: {0}",
            ["cs"] = "Konfigurace úspěšně obnovena z: {0}",
            ["sv"] = "Konfigurationen har återställts från: {0}"
        });

        AddRule(@"^Configuration exported to (.*)$", new()
        {
            ["tr"] = "Yapılandırma dışa aktarıldı: {0}",
            ["de"] = "Konfiguration exportiert nach: {0}",
            ["fr"] = "Configuration exportée vers : {0}",
            ["es"] = "Configuración exportada a: {0}",
            ["pt"] = "Configuração exportada para: {0}",
            ["it"] = "Configurazione esportata in: {0}",
            ["nl"] = "Configuratie geëxporteerd naar: {0}",
            ["pl"] = "Konfiguracja wyeksportowana do: {0}",
            ["ro"] = "Configurație exportată în: {0}",
            ["bg"] = "Конфигурацията е експортирана в: {0}",
            ["cs"] = "Konfigurace exportována do: {0}",
            ["sv"] = "Konfiguration exporterad till: {0}"
        });

        AddRule(@"^Configuration imported from (.*)$", new()
        {
            ["tr"] = "Yapılandırma içe aktarıldı: {0}",
            ["de"] = "Konfiguration importiert von: {0}",
            ["fr"] = "Configuration importée depuis : {0}",
            ["es"] = "Configuración importada desde: {0}",
            ["pt"] = "Configuração importada de: {0}",
            ["it"] = "Configurazione importata da: {0}",
            ["nl"] = "Configuratie geïmporteerd van: {0}",
            ["pl"] = "Konfiguracja zaimportowana z: {0}",
            ["ro"] = "Configurație importată din: {0}",
            ["bg"] = "Конфигурацията е импортирана от: {0}",
            ["cs"] = "Konfigurace importována z: {0}",
            ["sv"] = "Konfiguration importerad från: {0}"
        });

        AddRule(@"^Configuration file not found, creating default\.?$", new()
        {
            ["tr"] = "Yapılandırma dosyası bulunamadı, varsayılan oluşturuluyor.",
            ["de"] = "Konfigurationsdatei nicht gefunden, Standard wird erstellt.",
            ["fr"] = "Fichier de configuration introuvable, création du modèle par défaut.",
            ["es"] = "Archivo de configuración no encontrado, creando predeterminado.",
            ["pt"] = "Arquivo de configuração não encontrado, criando padrão.",
            ["it"] = "File di configurazione non trovato, creazione di quello predefinito.",
            ["nl"] = "Configuratiebestand niet gevonden, standaard wordt gemaakt.",
            ["pl"] = "Nie znaleziono pliku konfiguracyjnego, tworzenie domyślnego.",
            ["ro"] = "Fișierul de configurare nu a fost găsit, se creează cel implicit.",
            ["bg"] = "Конфигурационният файл не е намерен, създава се файл по подразбиране.",
            ["cs"] = "Konfigurační soubor nebyl nalezen, vytváří se výchozí.",
            ["sv"] = "Konfigurationsfilen hittades inte, standard skapas."
        });

        AddRule(@"^Failed to load configuration\.?$", new()
        {
            ["tr"] = "Yapılandırma yüklenemedi.",
            ["de"] = "Fehler beim Laden der Konfiguration.",
            ["fr"] = "Échec du chargement de la configuration.",
            ["es"] = "Error al cargar la configuración.",
            ["pt"] = "Falha ao carregar a configuração.",
            ["it"] = "Impossibile caricare la configurazione.",
            ["nl"] = "Laden van configuratie mislukt.",
            ["pl"] = "Nie udało się załadować konfiguracji.",
            ["ro"] = "Încărcarea configurației a eșuat.",
            ["bg"] = "Неуспешно зареждане на конфигурацията.",
            ["cs"] = "Nepodařilo se načíst konfiguraci.",
            ["sv"] = "Det gick inte att läsa in konfigurationen."
        });

        AddRule(@"^Starting PrintPilotProxy discovery service\.\.\.?$", new()
        {
            ["tr"] = "PrintPilotProxy keşif hizmeti başlatılıyor...",
            ["de"] = "PrintPilotProxy-Erkennungsdienst wird gestartet...",
            ["fr"] = "Démarrage du service de découverte PrintPilotProxy...",
            ["es"] = "Iniciando el servicio de descubrimiento de PrintPilotProxy...",
            ["pt"] = "Iniciando o serviço de descoberta do PrintPilotProxy...",
            ["it"] = "Avvio del servizio di rilevamento PrintPilotProxy...",
            ["nl"] = "PrintPilotProxy detectieservice wordt gestart...",
            ["pl"] = "Uruchamianie usługi wykrywania PrintPilotProxy...",
            ["ro"] = "Se pornește serviciul de descoperire PrintPilotProxy...",
            ["bg"] = "Стартиране на услугата за откриване на PrintPilotProxy...",
            ["cs"] = "Spouštění služby zjišťování PrintPilotProxy...",
            ["sv"] = "Startar PrintPilotProxy-identifieringstjänst..."
        });

        AddRule(@"^PrintPilotProxy discovery service started successfully on transport (.*)\.?$", new()
        {
            ["tr"] = "PrintPilotProxy keşif hizmeti {0} iletiminde başarıyla başlatıldı.",
            ["de"] = "PrintPilotProxy-Erkennungsdienst erfolgreich über Transport {0} gestartet.",
            ["fr"] = "Service de découverte PrintPilotProxy démarré avec succès sur le transport {0}.",
            ["es"] = "Servicio de descubrimiento PrintPilotProxy iniciado con éxito en transporte {0}.",
            ["pt"] = "Serviço de descoberta do PrintPilotProxy iniciado com sucesso no transporte {0}.",
            ["it"] = "Servizio di rilevamento PrintPilotProxy avviato con successo sul trasporto {0}.",
            ["nl"] = "PrintPilotProxy detectieservice succesvol gestart op transport {0}.",
            ["pl"] = "Usługa wykrywania PrintPilotProxy została pomyślnie uruchomiona w transporcie {0}.",
            ["ro"] = "Serviciul de descoperire PrintPilotProxy a pornit cu succes pe transportul {0}.",
            ["bg"] = "Услугата за откриване на PrintPilotProxy стартира успешно през транспорт {0}.",
            ["cs"] = "Služba zjišťování PrintPilotProxy byla úspěšně spuštěna na přenosu {0}.",
            ["sv"] = "PrintPilotProxy-identifieringstjänst startades framgångsrikt på transport {0}."
        });

        AddRule(@"^PrintPilotProxy UDP discovery transport started on port (\d+)\.?$", new()
        {
            ["tr"] = "PrintPilotProxy UDP keşif iletimi {0} portunda başlatıldı.",
            ["de"] = "PrintPilotProxy-UDP-Erkennungstransport auf Port {0} gestartet.",
            ["fr"] = "Transport de découverte UDP PrintPilotProxy démarré sur le port {0}.",
            ["es"] = "Transporte de descubrimiento UDP de PrintPilotProxy iniciado en el puerto {0}.",
            ["pt"] = "Transporte de descoberta UDP do PrintPilotProxy iniciado na porta {0}.",
            ["it"] = "Trasporto di rilevamento UDP PrintPilotProxy avviato sulla porta {0}.",
            ["nl"] = "PrintPilotProxy UDP-detectietransport gestart op poort {0}.",
            ["pl"] = "Transport wykrywania UDP PrintPilotProxy uruchomiony na porcie {0}.",
            ["ro"] = "Transportul de descoperire UDP PrintPilotProxy a pornit pe portul {0}.",
            ["bg"] = "UDP транспортът за откриване на PrintPilotProxy стартира на порт {0}.",
            ["cs"] = "UDP přenos zjišťování PrintPilotProxy byl spuštěn na portu {0}.",
            ["sv"] = "PrintPilotProxy UDP-identifieringstransport startad på port {0}."
        });

        AddRule(@"^Stopping PrintPilotProxy discovery service\.\.\.?$", new()
        {
            ["tr"] = "PrintPilotProxy keşif hizmeti durduruluyor...",
            ["de"] = "PrintPilotProxy-Erkennungsdienst wird beendet...",
            ["fr"] = "Arrêt du service de découverte PrintPilotProxy...",
            ["es"] = "Deteniendo el servicio de descubrimiento de PrintPilotProxy...",
            ["pt"] = "Parando o serviço de descoberta do PrintPilotProxy...",
            ["it"] = "Arresto del servizio di rilevamento PrintPilotProxy...",
            ["nl"] = "PrintPilotProxy detectieservice wordt gestopt...",
            ["pl"] = "Zatrzymywanie usługi wykrywania PrintPilotProxy...",
            ["ro"] = "Se oprește serviciul de descoperire PrintPilotProxy...",
            ["bg"] = "Спиране на услугата за откриване на PrintPilotProxy...",
            ["cs"] = "Zastavování služby zjišťování PrintPilotProxy...",
            ["sv"] = "Stoppar PrintPilotProxy-identifieringstjänst..."
        });

        AddRule(@"^PrintPilotProxy discovery service stopped\.?$", new()
        {
            ["tr"] = "PrintPilotProxy keşif hizmeti durduruldu.",
            ["de"] = "PrintPilotProxy-Erkennungsdienst beendet.",
            ["fr"] = "Service de découverte PrintPilotProxy arrêté.",
            ["es"] = "Servicio de descubrimiento PrintPilotProxy detenido.",
            ["pt"] = "Serviço de descoberta do PrintPilotProxy parado.",
            ["it"] = "Servizio di rilevamento PrintPilotProxy arrestato.",
            ["nl"] = "PrintPilotProxy detectieservice gestopt.",
            ["pl"] = "Usługa wykrywania PrintPilotProxy została zatrzymana.",
            ["ro"] = "Serviciul de descoperire PrintPilotProxy a fost oprit.",
            ["bg"] = "Услугата за откриване на PrintPilotProxy е спряна.",
            ["cs"] = "Služba zjišťování PrintPilotProxy byla zastavena.",
            ["sv"] = "PrintPilotProxy-identifieringstjänst stoppad."
        });

        AddRule(@"^PrintPilotProxy UDP discovery transport stopped\.?$", new()
        {
            ["tr"] = "PrintPilotProxy UDP keşif iletimi durduruldu.",
            ["de"] = "PrintPilotProxy-UDP-Erkennungstransport beendet.",
            ["fr"] = "Transport de découverte UDP PrintPilotProxy arrêté.",
            ["es"] = "Transporte de descubrimiento UDP de PrintPilotProxy detenido.",
            ["pt"] = "Transporte de descoberta UDP do PrintPilotProxy parado.",
            ["it"] = "Trasporto di rilevamento UDP PrintPilotProxy arrestato.",
            ["nl"] = "PrintPilotProxy UDP-detectietransport gestopt.",
            ["pl"] = "Transport wykrywania UDP PrintPilotProxy został zatrzymany.",
            ["ro"] = "Transportul de descoperire UDP PrintPilotProxy a fost oprit.",
            ["bg"] = "UDP транспортът за откриване на PrintPilotProxy е спрян.",
            ["cs"] = "UDP přenos zjišťování PrintPilotProxy byl zastaven.",
            ["sv"] = "PrintPilotProxy UDP-identifieringstransport stoppad."
        });

        AddRule(@"^PrintPilotProxy service worker starting\.?$", new()
        {
            ["tr"] = "PrintPilotProxy hizmet çalışanı başlatılıyor.",
            ["de"] = "PrintPilotProxy-Dienst-Worker startet.",
            ["fr"] = "Démarrage du processus de service PrintPilotProxy.",
            ["es"] = "Iniciando el trabajador de servicio de PrintPilotProxy.",
            ["pt"] = "Iniciando o worker do serviço PrintPilotProxy.",
            ["it"] = "Avvio del worker del servizio PrintPilotProxy.",
            ["nl"] = "PrintPilotProxy servicemedewerker start.",
            ["pl"] = "Uruchamianie procesu usługi PrintPilotProxy.",
            ["ro"] = "Se pornește procesul de serviciu PrintPilotProxy.",
            ["bg"] = "Стартиране на работния процес на услугата PrintPilotProxy.",
            ["cs"] = "Spouštění pracovního procesu služby PrintPilotProxy.",
            ["sv"] = "PrintPilotProxy-tjänstearbetare startar."
        });

        AddRule(@"^PrintPilotProxy service worker stopped\.?$", new()
        {
            ["tr"] = "PrintPilotProxy hizmet çalışanı durduruldu.",
            ["de"] = "PrintPilotProxy-Dienst-Worker beendet.",
            ["fr"] = "Processus de service PrintPilotProxy arrêté.",
            ["es"] = "Trabajador de servicio de PrintPilotProxy detenido.",
            ["pt"] = "Worker do serviço PrintPilotProxy parado.",
            ["it"] = "Worker del servizio PrintPilotProxy arrestato.",
            ["nl"] = "PrintPilotProxy servicemedewerker gestopt.",
            ["pl"] = "Proces usługi PrintPilotProxy został zatrzymany.",
            ["ro"] = "Procesul de serviciu PrintPilotProxy a fost oprit.",
            ["bg"] = "Работният процес на услугата PrintPilotProxy е спрян.",
            ["cs"] = "Pracovní proces služby PrintPilotProxy byl zastaven.",
            ["sv"] = "PrintPilotProxy-tjänstearbetare stoppad."
        });

        AddRule(@"^UnobtaniumProxyEngine is already running\.?$", new()
        {
            ["tr"] = "UnobtaniumProxyEngine zaten çalışıyor.",
            ["de"] = "UnobtaniumProxyEngine läuft bereits.",
            ["fr"] = "UnobtaniumProxyEngine est déjà en cours d'exécution.",
            ["es"] = "UnobtaniumProxyEngine ya está en ejecución.",
            ["pt"] = "UnobtaniumProxyEngine já está em execução.",
            ["it"] = "UnobtaniumProxyEngine è già in esecuzione.",
            ["nl"] = "UnobtaniumProxyEngine draait al.",
            ["pl"] = "UnobtaniumProxyEngine jest już uruchomiony.",
            ["ro"] = "UnobtaniumProxyEngine rulează deja.",
            ["bg"] = "UnobtaniumProxyEngine вече работи.",
            ["cs"] = "UnobtaniumProxyEngine již běží.",
            ["sv"] = "UnobtaniumProxyEngine körs redan."
        });

        AddRule(@"^UnobtaniumProxyEngine started on ['""]?([A-Za-z0-9_]+)['""]? mode on port (\d+)\.?$", new()
        {
            ["tr"] = "UnobtaniumProxyEngine \"{0}\" modunda {1} portunda başlatıldı.",
            ["de"] = "UnobtaniumProxyEngine im Modus \"{0}\" auf Port {1} gestartet.",
            ["fr"] = "UnobtaniumProxyEngine démarré en mode « {0} » sur le port {1}.",
            ["es"] = "UnobtaniumProxyEngine iniciado en modo \"{0}\" en el puerto {1}.",
            ["pt"] = "UnobtaniumProxyEngine iniciado no modo \"{0}\" na porta {1}.",
            ["it"] = "UnobtaniumProxyEngine avviato in modalità \"{0}\" sulla porta {1}.",
            ["nl"] = "UnobtaniumProxyEngine gestart in modus \"{0}\" op poort {1}.",
            ["pl"] = "UnobtaniumProxyEngine uruchomiony w trybie „{0}” na porcie {1}.",
            ["ro"] = "UnobtaniumProxyEngine a pornit în modul „{0}” pe portul {1}.",
            ["bg"] = "UnobtaniumProxyEngine стартира в режим „{0}“ на порт {1}.",
            ["cs"] = "UnobtaniumProxyEngine spuštěn v režimu „{0}“ na portu {1}.",
            ["sv"] = "UnobtaniumProxyEngine startades i läget \"{0}\" på port {1}."
        });

        AddRule(@"^UnobtaniumProxyEngine stopped\.?$", new()
        {
            ["tr"] = "UnobtaniumProxyEngine durduruldu.",
            ["de"] = "UnobtaniumProxyEngine beendet.",
            ["fr"] = "UnobtaniumProxyEngine arrêté.",
            ["es"] = "UnobtaniumProxyEngine detenido.",
            ["pt"] = "UnobtaniumProxyEngine parado.",
            ["it"] = "UnobtaniumProxyEngine arrestato.",
            ["nl"] = "UnobtaniumProxyEngine gestopt.",
            ["pl"] = "UnobtaniumProxyEngine został zatrzymany.",
            ["ro"] = "UnobtaniumProxyEngine a fost oprit.",
            ["bg"] = "UnobtaniumProxyEngine е спрян.",
            ["cs"] = "UnobtaniumProxyEngine byl zastaven.",
            ["sv"] = "UnobtaniumProxyEngine stoppad."
        });

        AddRule(@"^Failed to start UnobtaniumProxyEngine\.?$", new()
        {
            ["tr"] = "UnobtaniumProxyEngine başlatılamadı.",
            ["de"] = "Fehler beim Starten von UnobtaniumProxyEngine.",
            ["fr"] = "Échec du démarrage de UnobtaniumProxyEngine.",
            ["es"] = "Error al iniciar UnobtaniumProxyEngine.",
            ["pt"] = "Falha ao iniciar o UnobtaniumProxyEngine.",
            ["it"] = "Impossibile avviare UnobtaniumProxyEngine.",
            ["nl"] = "Starten van UnobtaniumProxyEngine mislukt.",
            ["pl"] = "Nie udało się uruchomić UnobtaniumProxyEngine.",
            ["ro"] = "Pornirea UnobtaniumProxyEngine a eșuat.",
            ["bg"] = "Неуспешно стартиране на UnobtaniumProxyEngine.",
            ["cs"] = "Nepodařilo se spustit UnobtaniumProxyEngine.",
            ["sv"] = "Det gick inte att starta UnobtaniumProxyEngine."
        });

        AddRule(@"^IPC server started on local pipe (.*)\.?$", new()
        {
            ["tr"] = "IPC sunucusu yerel kanalda ({0}) başlatıldı.",
            ["de"] = "IPC-Server auf lokaler Pipe {0} gestartet.",
            ["fr"] = "Serveur IPC démarré sur le canal local {0}.",
            ["es"] = "Servidor IPC iniciado en la canalización local {0}.",
            ["pt"] = "Servidor IPC iniciado no pipe local {0}.",
            ["it"] = "Server IPC avviato sulla pipe locale {0}.",
            ["nl"] = "IPC-server gestart op lokale pipe {0}.",
            ["pl"] = "Serwer IPC uruchomiony na lokalnym potoku {0}.",
            ["ro"] = "Serverul IPC a pornit pe canalul local {0}.",
            ["bg"] = "IPC сървърът стартира на локален канал {0}.",
            ["cs"] = "IPC server spuštěn na lokálním kanálu {0}.",
            ["sv"] = "IPC-server startad på lokal pipe {0}."
        });

        AddRule(@"^IPC server stopped\.?$", new()
        {
            ["tr"] = "IPC sunucusu durduruldu.",
            ["de"] = "IPC-Server beendet.",
            ["fr"] = "Serveur IPC arrêté.",
            ["es"] = "Servidor IPC detenido.",
            ["pt"] = "Servidor IPC parado.",
            ["it"] = "Server IPC arrestato.",
            ["nl"] = "IPC-server gestopt.",
            ["pl"] = "Serwer IPC został zatrzymany.",
            ["ro"] = "Serverul IPC a fost oprit.",
            ["bg"] = "IPC сървърът е спрян.",
            ["cs"] = "IPC server byl zastaven.",
            ["sv"] = "IPC-server stoppad."
        });

        AddRule(@"^Connected to local IPC server\.?$", new()
        {
            ["tr"] = "Yerel IPC sunucusuna bağlandı.",
            ["de"] = "Mit lokalem IPC-Server verbunden.",
            ["fr"] = "Connecté au serveur IPC local.",
            ["es"] = "Conectado al servidor IPC local.",
            ["pt"] = "Conectado ao servidor IPC local.",
            ["it"] = "Connesso al server IPC locale.",
            ["nl"] = "Verbonden met lokale IPC-server.",
            ["pl"] = "Połączono z lokalnym serwerem IPC.",
            ["ro"] = "Conectat la serverul IPC local.",
            ["bg"] = "Свързан към локалния IPC сървър.",
            ["cs"] = "Připojeno k lokálnímu IPC serveru.",
            ["sv"] = "Ansluten till lokal IPC-server."
        });

        AddRule(@"^Proxy request:\s*(\S+)\s+(\S+)\s+(\S+)\s*-\s*(\d+)$", new()
        {
            ["tr"] = "Proxy isteği: {0} {1} {2} - {3}",
            ["de"] = "Proxy-Anforderung: {0} {1} {2} - {3}",
            ["fr"] = "Requête proxy : {0} {1} {2} - {3}",
            ["es"] = "Solicitud proxy: {0} {1} {2} - {3}",
            ["pt"] = "Requisição proxy: {0} {1} {2} - {3}",
            ["it"] = "Richiesta proxy: {0} {1} {2} - {3}",
            ["nl"] = "Proxyverzoek: {0} {1} {2} - {3}",
            ["pl"] = "Żądanie proxy: {0} {1} {2} - {3}",
            ["ro"] = "Cerere proxy: {0} {1} {2} - {3}",
            ["bg"] = "Прокси заявка: {0} {1} {2} - {3}",
            ["cs"] = "Požadavek proxy: {0} {1} {2} - {3}",
            ["sv"] = "Proxybegäran: {0} {1} {2} - {3}"
        });

        AddRule(@"^Generated and saved new PrintPilotProxy instance ID:\s*(.*)$", new()
        {
            ["tr"] = "Yeni PrintPilotProxy örnek kimliği oluşturuldu ve kaydedildi: {0}",
            ["de"] = "Neue PrintPilotProxy-Instanz-ID generiert und gespeichert: {0}",
            ["fr"] = "Nouvel ID d'instance PrintPilotProxy généré et enregistré : {0}",
            ["es"] = "Nuevo ID de instancia de PrintPilotProxy generado y guardado: {0}",
            ["pt"] = "Novo ID de instância do PrintPilotProxy gerado e salvo: {0}",
            ["it"] = "Generato e salvato nuovo ID istanza PrintPilotProxy: {0}",
            ["nl"] = "Nieuwe PrintPilotProxy instantie-ID gegenereerd en opgeslagen: {0}",
            ["pl"] = "Wygenerowano i zapisano nowy identyfikator instancji PrintPilotProxy: {0}",
            ["ro"] = "S-a generat și salvat noul ID de instanță PrintPilotProxy: {0}",
            ["bg"] = "Генериран и записан нов ID на екземпляр на PrintPilotProxy: {0}",
            ["cs"] = "Vygenerováno a uloženo nové ID instance PrintPilotProxy: {0}",
            ["sv"] = "Nytt PrintPilotProxy-instans-ID genererades och sparades: {0}"
        });

        AddRule(@"^Removed managed Windows Firewall rule (.*)\.?$", new()
        {
            ["tr"] = "Yönetilen Windows Güvenlik Duvarı kuralı kaldırıldı: {0}",
            ["de"] = "Verwaltete Windows-Firewall-Regel entfernt: {0}",
            ["fr"] = "Règle de pare-feu Windows gérée supprimée : {0}",
            ["es"] = "Regla administrada del Firewall de Windows eliminada: {0}",
            ["pt"] = "Regra gerenciada do Firewall do Windows removida: {0}",
            ["it"] = "Regola del Windows Firewall gestita rimossa: {0}",
            ["nl"] = "Beheerde Windows Firewall-regel verwijderd: {0}",
            ["pl"] = "Usunięto zarządzaną regułę Zapory systemu Windows: {0}",
            ["ro"] = "Regula de firewall Windows gestionată a fost eliminată: {0}",
            ["bg"] = "Премахнато управлявано правило за защитна стена на Windows: {0}",
            ["cs"] = "Odebráno spravované pravidlo brány Windows Firewall: {0}",
            ["sv"] = "Hanterad Windows-brandväggsregel togs bort: {0}"
        });

        AddRule(@"^Updated managed Windows Firewall rule (.*) for protocol (.*)\.?$", new()
        {
            ["tr"] = "{1} protokolü için yönetilen Windows Güvenlik Duvarı kuralı ({0}) güncellendi.",
            ["de"] = "Verwaltete Windows-Firewall-Regel {0} für Protokoll {1} aktualisiert.",
            ["fr"] = "Règle de pare-feu Windows gérée {0} mise à jour pour le protocole {1}.",
            ["es"] = "Regla administrada del Firewall de Windows {0} actualizada para protocolo {1}.",
            ["pt"] = "Regra gerenciada do Firewall do Windows {0} atualizada para o protocolo {1}.",
            ["it"] = "Regola di Windows Firewall gestita {0} aggiornata per il protocollo {1}.",
            ["nl"] = "Beheerde Windows Firewall-regel {0} bijgewerkt voor protocol {1}.",
            ["pl"] = "Zaktualizowano zarządzaną regułę Zapory systemu Windows {0} dla protokołu {1}.",
            ["ro"] = "Regula de firewall Windows gestionată {0} a fost actualizată pentru protocolul {1}.",
            ["bg"] = "Актуализирано управлявано правило за защитна стена {0} за протокол {1}.",
            ["cs"] = "Aktualizováno spravované pravidlo brány Windows Firewall {0} pro protokol {1}.",
            ["sv"] = "Hanterad Windows-brandväggsregel {0} uppdaterades för protokoll {1}."
        });

        AddRule(@"^Skipping Windows Firewall rule configuration because process lacks administrator permission to manage Windows Firewall\.?$", new()
        {
            ["tr"] = "Süreç Windows Güvenlik Duvarı'nı yönetmek için yönetici iznine sahip olmadığından kural yapılandırması atlanıyor.",
            ["de"] = "Konfiguration der Windows-Firewall-Regel übersprungen, da dem Prozess Administratorrechte fehlen.",
            ["fr"] = "Configuration de la règle de pare-feu Windows ignorée car le processus n'a pas les droits d'administrateur.",
            ["es"] = "Omitiendo configuración de regla del Firewall de Windows porque el proceso carece de permisos de administrador.",
            ["pt"] = "Ignorando configuração de regra do Firewall do Windows porque o processo não tem permissão de administrador.",
            ["it"] = "Configurazione della regola di Windows Firewall ignorata perché il processo non dispone dei privilegi di amministratore.",
            ["nl"] = "Configuratie van Windows Firewall-regel overgeslagen omdat het proces beheerdersrechten mist.",
            ["pl"] = "Pomijanie konfiguracji reguły Zapory systemu Windows z powodu braku uprawnień administratora.",
            ["ro"] = "Se omite configurarea regulii de firewall Windows deoarece procesul nu are drepturi de administrator.",
            ["bg"] = "Пропуска се конфигурирането на защитната стена, тъй като процесът няма администраторски права.",
            ["cs"] = "Konfigurace pravidla brány Windows Firewall byla přeskočena, protože proces nemá oprávnění správce.",
            ["sv"] = "Hoppar över konfiguration av Windows-brandväggsregel eftersom processen saknar administratörsbehörighet."
        });

        AddRule(@"^Proxy engine recovery stopped after (\d+) attempt\(s\)\. The Windows Service remains available for local administration\.?$", new()
        {
            ["tr"] = "Proxy motoru kurtarma işlemi {0} denemeden sonra durduruldu. Windows Hizmeti yerel yönetim için kullanılabilir durumda kalır.",
            ["de"] = "Wiederherstellung der Proxy-Engine nach {0} Versuch(en) gestoppt. Der Windows-Dienst bleibt für die lokale Verwaltung verfügbar.",
            ["fr"] = "La récupération du moteur proxy s'est arrêtée après {0} tentative(s). Le service Windows reste disponible pour l'administration locale.",
            ["es"] = "La recuperación del motor proxy se detuvo después de {0} intento(s). El servicio de Windows sigue disponible para la administración local.",
            ["pt"] = "A recuperação do mecanismo de proxy parou após {0} tentativa(s). O serviço do Windows permanece disponível para administração local.",
            ["it"] = "Il ripristino del motore proxy è stato interrotto dopo {0} tentativo/i. Il servizio Windows rimane disponibile per l'amministrazione locale.",
            ["nl"] = "Herstel van proxy-engine gestopt na {0} poging(en). De Windows-service blijft beschikbaar voor lokaal beheer.",
            ["pl"] = "Odzyskiwanie silnika proxy zostało zatrzymane po {0} próbach. Usługa systemu Windows pozostaje dostępna do administrowania.",
            ["ro"] = "Recuperarea motorului proxy s-a oprit după {0} încercare/încercări. Serviciul Windows rămâne disponibil pentru administrare locală.",
            ["bg"] = "Възстановяването на прокси двигателя спря след {0} опита. Услугата на Windows остава достъпна за локално администриране.",
            ["cs"] = "Obnova proxy jádra byla zastavena po {0} pokusech. Služba systému Windows zůstává dostupná pro místní správu.",
            ["sv"] = "Återställning av proxymotorn stoppades efter {0} försök. Windows-tjänsten är fortfarande tillgänglig för lokal administration."
        });

        AddRule(@"^Retrying proxy engine start in (\d+) seconds\.?$", new()
        {
            ["tr"] = "Proxy motorunu başlatma {0} saniye içinde yeniden denenecek.",
            ["de"] = "Neuer Versuch zum Starten der Proxy-Engine in {0} Sekunden.",
            ["fr"] = "Nouvelle tentative de démarrage du moteur proxy dans {0} secondes.",
            ["es"] = "Reintentando iniciar el motor proxy en {0} segundos.",
            ["pt"] = "Tentando reiniciar o mecanismo de proxy em {0} segundos.",
            ["it"] = "Nuovo tentativo di avvio del motore proxy tra {0} secondi.",
            ["nl"] = "Opnieuw proberen de proxy-engine te starten over {0} seconden.",
            ["pl"] = "Ponowna próba uruchomienia silnika proxy za {0} sekund.",
            ["ro"] = "Se reîncearcă pornirea motorului proxy în {0} secunde.",
            ["bg"] = "Повторен опит за стартиране на прокси двигателя след {0} секунди.",
            ["cs"] = "Opakovaný pokus o spuštění proxy jádra za {0} sekund.",
            ["sv"] = "Försöker starta proxymotorn igen om {0} sekunder."
        });
    }

    private static void AddRule(string regex, Dictionary<string, string> translations)
    {
        Rules.Add(new Rule(new Regex(regex, RegexOptions.Compiled | RegexOptions.IgnoreCase), translations));
    }

    /// <summary>
    /// Translates the string if a matching rule exists for the culture.
    /// Returns the original string if culture is English or no rule matches.
    /// </summary>
    public static string Localize(string rawMessage, CultureInfo? culture = null)
    {
        if (string.IsNullOrWhiteSpace(rawMessage))
            return rawMessage;

        var targetCulture = culture ?? LocalizationService.Instance.CurrentCulture;
        var lang = targetCulture.TwoLetterISOLanguageName.ToLowerInvariant();

        if (lang == "en")
            return rawMessage;

        foreach (var rule in Rules)
        {
            var match = rule.Pattern.Match(rawMessage.Trim());
            if (match.Success)
            {
                if (rule.Templates.TryGetValue(lang, out var template))
                {
                    if (match.Groups.Count > 1)
                    {
                        var args = new object[match.Groups.Count - 1];
                        for (int i = 1; i < match.Groups.Count; i++)
                        {
                            args[i - 1] = match.Groups[i].Value;
                        }
                        try
                        {
                            return string.Format(targetCulture, template, args);
                        }
                        catch
                        {
                            return template;
                        }
                    }
                    return template;
                }
            }
        }

        return rawMessage;
    }
}
