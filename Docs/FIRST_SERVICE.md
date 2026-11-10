# Eerste dienst in Conductor Chaos

Open `Assets/Scenes/TrainPlayground.unity` en druk op Play. Kies **Spelen** voor solo of **Online hosten** en deel de Relay-code met de rest van de crew. Iedereen gebruikt dezelfde build. Kies **Tools → Conductor Chaos → Build Windows Client** om de lobby en beide dienstscènes samen te bouwen. Unity Authentication en Relay gebruiken het gekoppelde Unity Cloud-project.

## Route

- **ServiceIntroduction** begint in de briefingruimte van Whistleton. De crew loopt naar het perron, verplaatst een koffer, knipt een proefkaartje, oefent de deur en vertrekbel en rijdt een kort stukje. Er is geen tijdslimiet. Eén geslaagde handeling telt voor de hele crew.
- **ServiceLevel01** loopt van Bumblewick naar Puddleford. De host trekt één opdracht per run. De mogelijkheden zijn oma begeleiden, de wissel repareren, de kaartjes controleren, de rode koffer bezorgen en drie kratten vervoeren. De klok loopt vanaf het vertrekperron tot de trein stilstaat in het stopvak én het hoofdobjectief af is. Solo krijgt 5:00, duo 4:30, trio 4:00 en een crew van vier tot zes spelers 3:30.
- Deurtjes en gereedschap bedien je met de bestaande handgrip. Druk de gele vertrekknop op het perron met een hand omlaag. Houd voor het proefkaartje de kaartjestang met één hand vast en druk met de andere op het vrije handvat terwijl de bek het kaartje raakt. In de oma-opdracht open je de perrondeur en druk je bij oma op **E** om haar te begeleiden.

## Zelf stations plaatsen in Unity

1. Open de betreffende dienstscène. Plaats een station en voeg **Conductor Chaos → Station voor een dienst** toe. Kies **Vertrek** of **Aankomst** in de Inspector.
2. Gebruik bij vertrek een **Crew Spawn** op vloerniveau, een **Platform**-trigger bij de briefinguitgang, een **Train Stop**-trigger rond de beginpositie van de locomotief en een **Departure Bell** op het perron met een Box Collider op de beweegbare knop.
3. Zet bij aankomst een **Train Stop**-trigger rond de gewenste stoppositie van de locomotief. Geef de volledige trein perronruimte. Verplaats ook de eventuele aflevervakken en oma’s eindpunt als je het eindperron verplaatst.
4. Sleep de stations naar **Departure** en **Arrival** op het object **spelregels**. Stel de trein afzonderlijk in op het verbonden spoor. De gekleurde kaders verschijnen in de Scene View als je het station selecteert.

Beide dienstscènes staan in de buildlijst en zijn rechtstreeks te bewerken in Unity. De host bepaalt wanneer een volgende scène laadt. Medespelers sluiten aan voordat de host de dienst start.
