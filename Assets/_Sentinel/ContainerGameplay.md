# Konténer: mozgás, kamera, eső

Nyisd meg az `Assets/_Sentinel/Scenes/Lighting/LightingNew.unity` jelenetet,
majd indítsd el a Play módot. A jelenetben a meglévő kapszula a karakter
helyőrzője; a mozgás nem igényel animációs csomagot.

| Billentyű | Művelet |
| --- | --- |
| A / D vagy bal / jobb nyíl | Oldalirányú mozgás |
| W / S vagy fel / le nyíl | Mélységi mozgás |
| Space | Ugrás; rövidebb nyomással kisebb ugrás |
| Shift nyomva | Futás |
| Ctrl nyomva | Lassú séta |
| C nyomva | Guggolás és guggoló mozgás |

A guggolás elsőbbséget élvez a többi mozgásmóddal szemben. Alacsony akadály
alatt a karakter a C elengedése után is guggolva marad, amíg fel nem állhat.
A karakternek valódi CharacterController ütközése van, a modell felületein
MeshColliderek, a nyitott oldalon pedig láthatatlan mozgási határok vannak.

A `Player` gyökér a lábnál van, egységnyi skálával. A `Player Visual`
cserélhető modellre; a vizuális gyermek fordul és guggoláskor összemegy.
Az új `SentinelContainerPlayer` a korábbi, helyben található
`SentinelPlayerMozgas` script módosítása nélkül működik.

## Kamera

A `SentinelFollowCamera` a karaktert követi, kis előretekintéssel.
A `Container Gameplay Frame` a mozgó konténerhez kapcsolódik. A teljes
képkivágás korlátozása a nyitott oldal keretén belül tartja a képet, az
oldalsó széleket a hátsó fal síkján is ellenőrzi. A padló és a tető a mélyebb
sugarakat kitakarja, így a padló és a pocsolya látható marad.
A referencia +Z irányába néz; a végső korlátozás a
simítás után történik, így a konténer rázkódása sem nyit rést a kép szélén.
Széles képaránynál a kamera közelebb kerül, szükség esetén csökkenti a
látószöget. A kamera a 2.5D díszlet nyitott oldala előtt áll; a megjelenített
kép marad a belső falak határain belül.

## Eső

A `Roof Rain and Splashes` a tényleges tetőrés felett bocsát ki cseppeket.
A sugárvizsgálat kiszűri a fedett tetődarabokat. A mozgó cseppek ismét
ellenőrzik az útjukat, így a karakteren vagy más akadályon is becsapódnak.
A becsapódásokhoz fröccsenés és rövid, táguló gyűrű tartozik. A cseppek és
gyűrűk közös, újrahasznosított meshbe kerülnek, korlátozott elemszámmal.

A tetőrés alatti padló külön, a padló geometriáját követő nedves felületet
kapott. A HDRP Lit anyag magas smoothness értéke, a helyi reflection probe
és az SSR adja a visszafogott tükröződést. A reflection probe induláskor
frissül; az SSR a képen látható mozgásokat követi.

## Ellenőrzés

- `Sentinel > Validate container camera`: 360 kamerabeállítás, öt képarány,
  normál és megbillentett konténer, mindkét mélységi sík négy képsarka.
- A `SentinelContainerVerification.Run` Unity batch belépési pont Play módban
  ellenőrzi a mozgásmódokat, az átlós sebességet, a négy oldali ütközést,
  az ugrást, a talajfogást, a fej feletti akadályt és az eső becsapódásait.
  Képet ment a `Logs/container-gameplay.png` fájlba. Grafikus eszközt igényel;
  `-nographics` és `-quit` nélkül kell indítani, a végén maga lép ki.
- A `Sentinel > Set up container gameplay` egyszeri szerkesztői eszköz az
  eredeti jelenet bekötéséhez. A már bekötött jelenetet nem építi újra.

A régi `lvl1_container` jelenet nem az aktív pálya; ez a változtatás a
buildbe már bekapcsolt `LightingNew` jelenetet készíti elő.
