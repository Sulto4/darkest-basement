# Vocea: verificare Whisper

Generat de `tools/verify.py`. Fiecare replică e transcrisă cu Whisper large-v3 (Groq) în română; termenii englezești
neprinși în română se reverifică în engleză. Politica e zero diferențe: o replică cu cuvinte lipsă se regenerează
(`tools/vo.py <scena> --line N`) până trece.
**Rezultat: 103/103 replici trec; 6 regenerate.**

Variantele acceptate sunt doar englezisme scrise fonetic de Whisper (seed-ul → „sidul”, run → „ran”, sprite → „sprait”, View-ul → „viul”, try → „trai”, Gaper → „Gapar”, Block-ul → „blocul”, Monstro → „Monstruo”) sau cratime („joac-o” → „joacao”); în plus „doi și jumătate” transcris „2,5” și o dată „cu unu” auzit „cu unul” (s06/8, același sens). Patru replici în care Whisper a auzit alt cuvânt românesc (construit, pornit, schimb, pixele) au fost reformulate și regenerate, chiar dacă două erau doar „i”-ul final abia rostit pe care Whisper îl pierde. Vocea: Bogdan Gamaleț, `eleven_multilingual_v2`.


## s01 (7 replici, 31.6 s de vorbire)

| # | rezultat | Whisper (ro) |
|---|---|---|
| 1 | OK | Asta e Darkest Basement. |
| 2 | OK | Un joc pe ture în care cobori cu patru eroi într-un subsol plin de monștri. |
| 3 | OK | Alegi o cameră pe hartă, mergi pe hol, deschizi ușa și te bați cu cărți. |
| 4 | OK | Fiecare erou are pachetul lui. Câștigi o luptă, primești o carte nouă și cobori mai jos. |
| 5 | OK | Trei etaje, trei boși, iar la final te așteaptă mom. |
| 6 | OK (regenerată) | În videoul ăsta îl desfacem bucată cu bucată. Cum e gândit ce matematică stă în spate și cum l-ai putea construi tu de la zero? |
| 7 | OK | O să vezi că aproape fiecare piesă e o idee pe care o știi deja de la facultate, doar că pusă la treabă. |

## s02 (10 replici, 66.8 s de vorbire)

| # | rezultat | Whisper (ro) |
|---|---|---|
| 1 | OK | Prima decizie e și cea mai importantă. Jocul are două lumi separate. |
| 2 | OK | Prima lume e modelul, el e creierul. Știe regulile, ține minte câtă viață are fiecare și ce cărți are în mână. |
| 3 | OK (variante: spriteuri→spraituri) | Modelul nu știe că există Unity. N-are spraituri, n-are cameră, n-are sunet. E C-sharp curat, doar numere și liste. |
| 4 | OK (variante: viewul→viul) | A doua lume e viul. Ăsta e teatrul de păpuși. Desenează, animă, cântă și ascultă ce face jucătorul. |
| 5 | OK | Între ele circulă două feluri de mesaje. View-ul trimite comenzi. De exemplu, joacă cartea asta pe ținta asta. |
| 6 | OK | Modelul calculează tot pe loc și răspunde cu o listă de evenimente. Cartea a fost jucată. Gaper a luat 5 damage. Gaper a murit. |
| 7 | OK | View-ul ia lista și o povestește pe ecran, eveniment cu eveniment. |
| 8 | OK | De ce ne complicăm așa? Pentru că un model fără grafică îl poți testa în câteva milisecunde. |
| 9 | OK | Poți să joci sute de run-uri complete în consolă, fără să deschizi Unity. Revenim la asta mai târziu. |
| 10 | OK | Și regula nu e doar o promisiune. Modelul stă într-un assembly separat, care n-are voie să vadă motorul. Dacă încerci să folosești ceva din Unity acolo, codul nu compilează. |

## s03 (7 replici, 39.8 s de vorbire)

| # | rezultat | Whisper (ro) |
|---|---|---|
| 1 | OK | Bun. Cum curge jocul de la meniu până la final? |
| 2 | OK | Gândește-te la el ca la un automat finit, ca la cursul de limbaje formale. |
| 3 | OK | La orice moment ești într-o singură stare. Meniu, hartă, hol, cameră, luptă, recompensă. |
| 4 | OK | Fiecare stare e o corutină. O joci de la cap la coadă, iar la final ea spune care e starea următoare. |
| 5 | OK | Din hartă pleci pe hol. De pe hol intri în cameră. După luptă iei recompensa și te întorci pe hartă. |
| 6 | OK | Toată bucla asta e un while cu un switch. Atât conduce un run întreg. |
| 7 | OK | Și GameFlow nu decide nimic. Modelul spune ce e în cameră și cine moare. GameFlow doar pune în scenă și așteaptă alegerea ta. |

## s04 (10 replici, 69.3 s de vorbire)

| # | rezultat | Whisper (ro) |
|---|---|---|
| 1 | OK | Harta unui etaj e un graf. Camerele sunt noduri pe o grillă de 5x4, iar holurile sunt muchiile. |
| 2 | OK | Cum o generăm? Pornim dintr-o cameră aleasă la întâmplare. |
| 3 | OK | La fiecare pas ne uităm la locurile libere de lângă camerele deja puse. Alegem unul la întâmplare și îl legăm de vecinul lui. |
| 4 | OK | Fiecare cameră nouă vine cu exact o muchie. Deci N camere au N-1 muchii și totul e conectat, adică un arbore de acoperire. |
| 5 | OK | Doar că într-un arbore, între două camere, există un singur drum. N-ai ce alege. Așa că mai adăugăm una sau două muchii. Apar bucle, deci apar alegeri. |
| 6 | OK | Cu o condiție. O muchie nouă intră doar dacă rămân măcar două fundături, pentru că ne trebuie locuri pentru bos și pentru comoară. |
| 7 | OK | Acum, unde punem BOS-ul? Cât mai departe de Start. |
| 8 | OK | Aici intră BFS-ul. Startul are distanța 0, vecinii lui 1, vecinii vecinilor 2. Și tot așa, cu o coadă. |
| 9 | OK | Camera cu distanța cea mai mare devine boss-ul. La egalitate alegem o fundătură, ca să nu dai peste boss în drum spre altceva. |
| 10 | OK | Comoara merge în altă fundătură. Restul camerelor se împart în lupte, un pat pentru odihnă și un curio. |

## s05 (10 replici, 79.2 s de vorbire)

| # | rezultat | Whisper (ro) |
|---|---|---|
| 1 | OK (termeni confirmați în engleză) | Acum, norocul. Un roglaic trebuie să fie altfel la fiecare ran, dar noi vrem să-l putem și repeta. |
| 2 | OK (termeni confirmați în engleză) | Soluția e un seed, un singur număr de la care pornește tot. Același seed înseamnă același ran, pas cu pas. |
| 3 | OK (variante: sedul→sidul) | Asta te salvează la teste. Dacă un bug apare pe sidul 7, îl reproduci de câte ori vrei. |
| 4 | OK | În spate e un generator pseudo-aleator foarte mic. Ia un număr pe 32 de biți și îl amestecă cu 3 deplasări de biți și sau exclusiv. |
| 5 | OK | Nu e aleator de adevăratelea. E o funcție deterministă care doar arată aleator. |
| 6 | OK | Un detaliu fin. Ca să alegi un număr între 0 și N, nu iei pur și simplu restul împărțirii, pentru că unele resturi ar ieși mai des. Codul aruncă câteva valori și mai încearcă odată. |
| 7 | OK (variante: sedul→sidul) | Apoi fluxuri separate. Din sidul principal facem 3 generatoare. Unul pentru hartă, unul pentru luptă și unul pentru recompense. |
| 8 | OK (variante: runului→ranului) (regenerată) | Așa, dacă se schimbă ceva în luptă, harta rămâne la fel. Altfel, o singură aruncare de zar în plus ar muta tot restul ranului. |
| 9 | OK | Pentru pachetul de cărți folosim Fisher-Yates. Mergi de la ultima carte spre prima. Pentru fiecare poziție îi alegi un J între 0 și I și le schimbi între ele. |
| 10 | OK (regenerată) | Toate cele 120 de permutări ale celor 5 cărți ies cu aceeași probabilitate. Și sunt trei rânduri de cod. |

## s06 (9 replici, 69.9 s de vorbire)

| # | rezultat | Whisper (ro) |
|---|---|---|
| 1 | OK | Lupta. Fiecare rundă începe cu inițiativa. Fiecare personaj aruncă un zar de la 0 la 3 și îl adună la viteza lui. |
| 2 | OK | Apoi sortăm descrescător. Asta e toată ordinea turelor. O sortare. |
| 3 | OK | La egalitate merg întâi eroii, iar dacă tot e egal, decide idul. Comparatorul trebuie să fie o ordine totală, pentru că sortarea din C-sharp nu e stabilă. Altfel, aceeași rundă ar putea ieși diferit. |
| 4 | OK | Damage-ul e o compunere de funcții. Pornești de la baza cărții și aduni strength-ul. |
| 5 | OK | Dacă atacatorul e slăbit, înmulțești cu 0,75. Dacă ținta e marcată, cu 1,5. Critul, încă o dată 1,5. |
| 6 | OK (variante: blockul→blocul) | Rotunjești în jos și abia la final scazi blocul. |
| 7 | OK | Fiecare pas e o funcție mică. Le poți testa separat, dar nu le poți muta oricum, pentru că rotunjirea nu comută cu înmulțirea. |
| 8 | OK (variante: unu→unul) | Statusurile sunt doar 8 numere într-un vector Block, bleed, poison, stun și celelalte Cele mai multe scad cu unul pe tură |
| 9 | OK (variante: gaper→gapar) | iar inamicii își arată intenția de la începutul rundei. Știi dinainte pe cine vrea Gapăr să muște. Asta transformă lupta într-un puzzle. |

## s07 (6 replici, 42.6 s de vorbire)

| # | rezultat | Whisper (ro) |
|---|---|---|
| 1 | OK | Când joci o carte, modelul rezolvă totul pe loc. Damage, morți, mutări, statusuri. Într-o fracțiune de milisecundă. |
| 2 | OK | Dar dacă le-ai arăta pe toate deodată, n-ai înțelege nimic. Așa că view-ul le pune la coadă. |
| 3 | OK | Ia primul eveniment, îl animă, așteaptă să se termine, apoi trece la următorul. |
| 4 | OK | În Unity asta se face cu corutine. O funcție care face yield return se oprește acolo și continuă când e gata animația. |
| 5 | OK | Un switch alege animația pentru fiecare tip de eveniment. Damaged înseamnă un număr care sare, o clipire albă, puțin sânge și un shake. |
| 6 | OK (variante: try→trai) | Și încă ceva. Dacă o animație aruncă o excepție, lupta merge mai departe. Fiecare pas e învelit într-un trai. Un bug vizual n-are voie să-ți blocheze jocul. |

## s08 (8 replici, 58.9 s de vorbire)

| # | rezultat | Whisper (ro) |
|---|---|---|
| 1 | OK | Cum știi dacă jocul e prea greu? Nu-l joci tu de o mie de ori. Îl pui pe calculator să-l joace. |
| 2 | OK | Auto Player e un jucător simplu și lacom. Dă damage unde contează, se vindecă atunci când e jos și pune block când vede că vine o lovitură. |
| 3 | OK | Pentru că modelul nu depinde de Unity, simulatorul joacă 500 de run-uri complete în câteva secunde. |
| 4 | OK | Asta e metoda Monte Carlo. Nu calculezi probabilitatea exact. O estimezi din foarte multe încercări. |
| 5 | OK | Prima versiune a dat 0 victorii din 500. Luptele se terminau în două runde și ceva. Prea brutal. |
| 6 | OK | Am crescut viața inamicilor cu vreo 50% și am scăzut damage-ul. Acum botul câștigă 219 din 500, adică 40 și 3,8%. |
| 7 | OK | O luptă normală ține în medie 3 runde și jumătate, iar una cu boss aproape 7. Exact cât ne-am propus. |
| 8 | OK (variante: runuri→ranuri, seduri→siduri) | Cu 500 de ranuri, eroarea e de câteva procente. De asta am verificat și pe alte siduri. A ieșit cam 50%, tot în țintă. |

## s09 (14 replici, 103.7 s de vorbire)

| # | rezultat | Whisper (ro) |
|---|---|---|
| 1 | OK | Acum partea care face un joc să se simtă bine. Și e mai multă matematică decât ai crede. |
| 2 | OK | Totul pornește de la lerp. Interpolare liniară. A plus B minus A ori T. |
| 3 | OK | Pentru T de la 0 la 1 mergi pe segment de la A la B. Dacă T crește uniform, mișcarea arată robotic. Pornește brusc și se oprește brusc. |
| 4 | OK | Trucul e să treci T printr-o funcție de easing. O funcție de la 0, 1 în 0, 1 cu f de 0 egal cu 0 și f de 1 egal cu 1. |
| 5 | OK (termeni confirmați în engleză) | IZOUT cubic pleacă repede și frânează moale. E 1 minus 1 minus T, la puterea a treia. |
| 6 | OK | Ease Out Back trece puțin de 1 și revine. De aici vine senzația de pop. |
| 7 | OK | Când un erou atacă, întâi se trage puțin înapoi, ca un arc care se încarcă. Apoi sare spre țintă pe ease-out cubic, într-o zecime de secundă, și se întoarce acasă lin. |
| 8 | OK | Pe deasupra se turtește și se lungește puțin, iar când stă, respiră. Scara lui pe verticală urmează un sinus. |
| 9 | OK | Camera folosește Smooth Damp. Gândește-te la un arc cu amortizare critică. Ajunge la țintă cât mai repede, fără să treacă de ea și fără să oscileze. |
| 10 | OK | Pe hol, camera urmărește echipa cu un timp de netezire de 0,22 de secunde. Ținta sare, camera nu. |
| 11 | OK | și shake-ul. Fiecare lovitură adaugă trauma, un număr între 0 și 1 care scade în timp. |
| 12 | OK | Camera nu tremură cu trauma, ci cu trauma la pătrat. Loviturile mici abia se simt, cele mari se simt mult. |
| 13 | OK | Direcția vine din zgomot perlin, nu din random. Zgomotul e continuu, așa că imaginea tremură, nu sare dintr-un loc în altul. |
| 14 | OK | Pune peste asta un hit stop, timpul înghețat câteva zeci de milisecunde, și creierul citește lovitura ca având greutate. |

## s10 (8 replici, 65.7 s de vorbire)

| # | rezultat | Whisper (ro) |
|---|---|---|
| 1 | OK | Holul e 3D adevărat, dar personajele sunt plate. Sunt billboard-uri. Un dreptunghi cu sprite-ul pe el. |
| 2 | OK (regenerată) | La fiecare frame îl rotim după cameră. Iei direcția în care se uită camera, pui componenta verticală pe 0 ca să rămână drept și îl aliniezi cu ea. |
| 3 | OK (variante: spriteurile→spraiturile) | Atenție, nu se uită spre cameră. E paralel cu planul ei. Așa spraiturile de pe margini nu se deformează. |
| 4 | OK | Când intri în luptă, camera iese din spatele echipei pe un arc, ca o macară. Unghiul și raza se interpolează separat, iar înălțimea mai primește un sinus care o ridică la mijloc. |
| 5 | OK | Acum pixeli. 32 de pixeli de artă fac o unitate în lume. Interfața e desenată la 640x360. |
| 6 | OK | Apoi o mărim cu un factor întreg, pe Full HD, ori 3. Fiecare pixel devine un pătrat perfect de 3x3. |
| 7 | OK (variante: jumatate→virgula) (regenerată) | Cu un factor ca 2,5, unele coloane ar ieși mai late decât altele și se vede imediat. De asta doar numere întregi, iar ce rămâne pe margini e bandă neagră. |
| 8 | OK | Chiar și camera de luptă e calculată așa. Vede exact 10 unități în lățime, deci fiecare pixel de artă iese 2x2. |

## s11 (9 replici, 64.2 s de vorbire)

| # | rezultat | Whisper (ro) |
|---|---|---|
| 1 | OK (regenerată) | Bun, dacă ar fi să pornești de la zero, în ce ordine ai învăța? |
| 2 | OK | 1. C-Sharp simplu, fără Unity. Clase, liste, dicționare. Scrie o luptă în consolă cu doi eroi și un inamic. Exact ce face simulatorul. |
| 3 | OK | 2. Unity de bază Ce e un GameObject? Ce e o componentă? Ce face update? Pune un sprite care respiră cu un sinus. |
| 4 | OK | 3. Leagă cele două lumi cu o coadă de evenimente Lupta ta din consolă produce evenimente, iar o corutină le animă una câte una. |
| 5 | OK | 4. Interfața. Un canvas. Câteva butoane. O carte care se ridică atunci când treci cu mouse-ul peste ea. |
| 6 | OK | 5. Camera și mișcarea. Lerp, easing, smooth damp. Fă holul văzut din spate și tranziția în lateral. |
| 7 | OK | 6. Harta. BFS și arborele de acoperire, desenate pe o grillă. |
| 8 | OK | 7. Echilibrarea Lași un bot să joace, strângi statistici și ajustezi cifrele. |
| 9 | OK | Regula care leagă totul e simplă. Fiecare pas se poate testa singur. Așa rămâne un proiect mare sub control. |

## s12 (5 replici, 29.3 s de vorbire)

| # | rezultat | Whisper (ro) |
|---|---|---|
| 1 | OK | ca să-l faci al tău 3 exerciții. |
| 2 | OK (variante: joaco→joacao) | 1. Adaugă o carte nouă în Car Database și joacă-o. Textul ei se scrie singur din efecte, fără nici o linie nouă de interfață. |
| 3 | OK | 2. Schimbă curba de easing de la tranziția camerei. Pune ease out back sau chiar liniar și simte diferența. |
| 4 | OK (variante: monstro→monstruo) | 3. Dublează viața lui Monstruo, rulează simulatorul și citește noua rată de victorie. |
| 5 | OK | Dacă știi să explici de ce s-a schimbat cifra, ai înțeles jocul. Spor la coborât! |

