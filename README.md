# Wi-Fi-vaihtaja

Yhden napsautuksen Wi-Fi-vaihtaja Windows 10/11 -tietokoneille. Valitse asennuksessa kaksi tallennettua Wi-Fi-verkkoa ja kiinnitä vaihto tehtäväpalkkiin.

**Asennusohje ja lähdekoodi:** https://ljrant.github.io/Wifi-Swapper/

## Asennus

1. Yhdistä molempiin Wi-Fi-verkkoihin Windowsissa vähintään kerran.
2. Lataa [ZIP-paketti](https://github.com/ljrant/Wifi-Swapper/archive/refs/heads/main.zip) ja pura se.
3. Käynnistä `Install.cmd` puretussa kansiossa.
4. Valitse **Verkko A** ja **Verkko B** ja paina **Tallenna**.
5. Etsi Käynnistä-valikosta **Wi-Fi-vaihtaja** ja kiinnitä se tehtäväpalkkiin.
6. Napsauta tehtäväpalkin kuvaketta vaihtaaksesi verkkoa.

Valittuja verkkoja voi myöhemmin muuttaa Käynnistä-valikon **Vaihda Wi-Fi-vaihtajan verkot** -pikakuvakkeesta.

## Ilmaisin

Ilmoitusalueen pieni kuvake kertoo verkon: **A**, **B** tai **?**, jos verkkoa ei voi varmistaa. Osoita kuvaketta hiirellä nähdäksesi nykyisen verkon nimen. Kun uusi yhteys havaitaan, Windows näyttää lyhyen ilmoituksen (jos ilmoitukset on sallittu). Ilmaisin käynnistyy kirjautuessa.

## Tekniikka

Ohjelma käyttää Windows WLAN API:a. Salasanoja ei kerätä tai tallenneta. Molempien Wi-Fi-verkkojen täytyy olla valmiiksi tallennettuja ja kuuluvuusalueella. Asennus kääntää C#-lähdekoodin paikallisesti .NET Framework 4.x -kääntäjällä eikä vaadi ylläpitäjän oikeuksia.

**Huom.** Sovellusta ei ole testattu oikealla Windows-laitteella.

## Lähdekoodi

- [WiFiToggle.cs](WiFiToggle.cs)
- [Install.ps1](Install.ps1)
- [Install.cmd](Install.cmd)
- [index.html](index.html)

## GitHub Pages

Sivu julkaistaan `.github/workflows/pages.yml`-työnkululla. Aktivoi tarvittaessa GitHubissa **Settings → Pages → Build and deployment → Source: GitHub Actions**.
