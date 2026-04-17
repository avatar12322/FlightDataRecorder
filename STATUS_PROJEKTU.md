TYTUŁ PROJEKTU: Projekt i implementacja systemu analizy i oceny jakości podejścia do
lądowania samolotów cywilnych na podstawie danych telemetrycznych z symulatora
lotu
OPIS PROJEKTU: Projekt polega na stworzeniu aplikacji desktopowej działającej
lokalnie na komputerze użytkownika, analizującej w czasie rzeczywistym dane
telemetryczne z symulatora lotu Microsoft Flight Simulator 2024 (MSFS). System skupia
się na ocenie bezpieczeństwa i jakości fazy podejścia oraz lądowania samolotem
pasażerskim (Airbus A350). Aplikacja integruje się z symulatorem poprzez interfejs
SimConnect, buforuje logi z ostatnich 60–120 sekund lotu, analizuje je i generuje raport.
Raport zawiera ocenę punktową (0–100) opartą na modelu sztucznej sieci neuronowej
oraz precyzyjne wskazówki tekstowe oparte na oficjalnych procedurach (SOP) linii
lotniczych. System zapisuje historię prób, pozwalając na śledzenie postępów
użytkownika.
CELE PROJEKTU:
• automatyczna ekstrakcja i analiza danych telemetrycznych z symulatora w czasie
rzeczywistym za pomocą API SimConnect (próbkowanie z częstotliwością 10-100
Hz)
• ocena jakości pilotażu z wykorzystaniem podejścia hybrydowego (system
regułowy + uczenie maszynowe)
• wykrywanie błędów w oparciu o wiedzę domenową i kryteria "Ustabilizowanego
Podejścia" (Stabilized Approach wg instrukcji FCOM Airbusa)
• generowanie czytelnych raportów tekstowych dla użytkownika oraz archiwizacja
historii lotów
• praktyczne zastosowanie głębokich sieci neuronowych do analizy szeregów
czasowych (regresja/modele sekwencyjne) w środowisku zintegrowanym z
technologiami .NET
PROBLEM INŻYNIERSKI: Problem polega na asynchronicznym pozyskaniu i
przekształceniu surowych strumieni danych telemetrycznych (niskopoziomowych
zmiennych symulacyjnych SimVars) w ustrukturyzowane zbiory cech (Feature
Engineering), włączając w to detekcję zdarzeń (np. moment flare i touchdown). System
musi zautomatyzować ten proces, zniwelować różnice w skalach (normalizacja),
zastosować ocenę bazującą na fizyce i ograniczeniach płatowca, a na koniec
udostępnić wynik w przystępnej aplikacji okienkowej.
ZAKRES PROJEKTU:
• analiza jednego głównego scenariusza: podejście do lądowania (Approach &
Landing) maszyny Airbus A350

• projekt i implementacja modułu klienta SimConnect w języku C#
• projekt i implementacja aplikacji desktopowej z lokalną bazą danych
• trenowanie modelu głębokiej sieci neuronowej w środowisku chmurowym i
implementacja z użyciem formatu ONNX
• generowanie raportów i wykresów postępu
TECHNOLOGIE:
• język aplikacji: C#
• język analizy i trenowania ML: Python
• platforma: .NET (WPF, .NET MAUI lub Blazor Desktop)
• baza danych: SQLite (lokalna baza plików)
• ORM: Entity Framework Core
• wizualizacja danych: np. LiveCharts / Chart.js
• integracja z symulatorem: Microsoft.FlightSimulator.SimConnect.dll (SDK MSFS)
• ML (Środowisko Hybrydowe): Google Colab (TensorFlow / scikit-learn) do
treningu -> format ONNX -> wdrożenie przez ML.NET w C#
ARCHITEKTURA SYSTEMU:
1. Moduł pozyskiwania danych (Data Acquisition):
• klient SimConnect napisany w C# subskrybujący strukturę zmiennych lotu (10-
100 Hz) i logujący je do bufora pamięci w trakcie zniżania.
2. Moduł bazodanowy i konfiguracji:
• lokalna baza SQLite przechowująca historię prób, parametry lądowań i profil
użytkownika.
3. Moduł analizy danych i systemu regułowego (Backend C#):
• wstępne przetwarzanie (preprocessing) szeregów czasowych.
• inżynieria cech (wyliczanie m.in. płynności sterowania, RMS odchylenia od ILS).
• generowanie twardej oceny regułowej (kary za złamanie SOP) i tekstowego
feedbacku.
4. Moduł ML:
• silnik ML.NET wczytujący wytrenowany model ONNX.
• inferencja: przewidywanie ostatecznej oceny techniki pilotażu i "płynności" lotu.

5. Moduł interfejsu i raportowania (Frontend):
• okno aplikacji wyświetlające końcowy wynik, statystyki z momentu przyziemienia
i wykresy.
METODY ANALIZY (ZASADY OCENY LOTU A350):
1. System regułowy (Etykietowanie i Feedback):
• Start z puli 100 punktów.
• Bramki stabilności (Stabilization Gates): -50 pkt za brak konfiguracji do lądowania
(podwozie, klapy FULL) na wysokości 1000 ft.
• Prędkość opadania przy przyziemieniu (Sink Rate at Touchdown): weryfikacja tzw.
Hard Landing (np. kara -30 pkt za sink rate > 600 fpm).
• Ścieżka schodzenia i lokalizator: kary za odchylenia (Glideslope/Localizer
Deviation).
• Zarządzanie ciągiem: opóźnione ustawienie przepustnicy na IDLE po komendzie
"RETARD".
2. Uczenie maszynowe:
• zadanie: regresja (przewidywanie płynności operowania sterami -
Smoothness/Landing score).
• architektura modelu: sieć neuronowa przystosowana do szeregów czasowych
(np. sieć sekwencyjna lub rozbudowany MLP na uśrednionych oknach
czasowych) trenowana na wyekstrahowanych metrykach lotu.
DANE:
• dane generowane z symulatora MSFS 2024.
• 6 DOF Kinematyka: Wysokość (RA, Baro), Prędkości (IAS, GS, V/S), Pitch, Roll,
Yaw.
• Parametry lądowania: Glide slope deviation, Localizer deviation, Pitch/Bank at
touchdown, Sink rate.
• Konfiguracja: Flaps, Gear, Throttle position.
• Warunki: Crosswind, Visibility.
• Inputy Pilota: Sidestick X/Y, Rudder, Brake pressure.
UCZENIE MODELU:

• dane oznaczane automatycznie przez skrypt oparty o system regułowy,
wyliczający obiektywny "Ground Truth" (punkty karne) na podstawie zebranych
plików.
• proces uczenia i optymalizacji hiperparametrów odbywa się w Google Colab.
WYMAGANIA PRACY INŻYNIERSKIEJ:
• analiza problemu oceny jakości podejścia w lotnictwie cywilnym.
• przegląd i dobór technologii komunikacyjnych (SimConnect vs rozwiązania
alternatywne).
• opis procesu inżynierii cech (Feature Engineering) na surowych szeregach
czasowych.
• implementacja systemu, testy w symulatorze i wnioski z predykcji.
STRUKTURA PRACY:
1. Wstęp (cel pracy, problematyka bezpieczeństwa w fazie podejścia).
2. Część teoretyczna (specyfika danych telemetrycznych z symulatorów, zasady
ustabilizowanego podejścia w lotnictwie cywilnym, metody uczenia
maszynowego).
3. Część projektowa (architektura integracji przez SimConnect, inżynieria cech,
implementacja logiki eksperckiej i modelu ML).
4. Testy i analiza (ocena precyzji wirtualnego instruktora, wyniki predykcji modelu).
5. Zakończenie (wnioski, możliwości rozbudowy o inne samoloty pasażerskie, np. z
rodziny Boeing).
ZAŁOŻENIA I CEL KOŃCOWY: Aplikacja działa w tle podczas rozgrywki w MSFS.
Użytkownik nie musi ręcznie oznaczać błędów – to system regułowy działa jako
"wirtualny Kapitan/Instruktor", sprawdzający limity bezpieczeństwa. Model ML dodaje
do tego analizę ukrytych wzorców (technika operowania drążkiem, płynność), by na
koniec zaprezentować spójną punktację. Celem jest budowa profesjonalnego,
inżynierskiego potoku danych, który z obiektywnej fizyki symulatora robi czytelne
narzędzie treningowe.

DOKUMENTACJA PROJEKTOWA: System Analizy Telemetrii (MSFS 2024)Data ostatniej aktualizacji: 09.04.2026Status: Etap I (Akwizycja danych) – ZAKOŃCZONY SUKCESEM.1. Cel i Zakres ProjektuCelem pracy jest budowa systemu, który w czasie rzeczywistym pobiera dane z symulatora lotu, ocenia jakość podejścia do lądowania (SOP - Standard Operating Procedures) oraz wykorzystuje model uczenia maszynowego do predykcji błędów pilotażu.Platforma: Microsoft Flight Simulator 2024.Statek powietrzny: Airbus A320neo (wybrany ze względu na natywną obsługę zmiennych SimConnect).Technologia: C# .NET 10 (WPF), Python (ML - Google Colab).2. Architektura Systemu (Implementacja C#)Aplikacja desktopowa łączy się z symulatorem poprzez interfejs SimConnect. Rozwiązano kluczowe problemy integracyjne:Architektura x64: Wymuszono kompilację projektu pod systemy 64-bitowe, aby zapewnić zgodność z bibliotekami MSFS.Zarządzanie zależnościami: Plik .csproj został ręcznie skonfigurowany, aby automatycznie kopiować biblioteki Microsoft.FlightSimulator.SimConnect.dll (Wrapper) oraz SimConnect.dll (Native) do folderu wynikowego.3. Definicja Wektora Danych (Telemetry Data)Zdefiniowano precyzyjną strukturę TelemetryStruct, która mapuje 19 zmiennych fizycznych symulatora. Kolejność w kodzie (AddToDataDefinition) została zsynchronizowana ze strukturą pamięci, co eliminuje błędy przesunięcia danych.Zestawienie zmiennych:Altitude / RadioAlt – Wysokość bezwzględna i nad terenem.Airspeed / VerticalSpeed – Prędkość pozioma i pionowa.Pitch / Bank – Kąty pochylenia i przechylenia.Gear / Flaps – Konfiguracja podwozia i klap.Weight – Aktualna masa samolotu (ważna dla dynamiki lądowania).TouchdownVelocity – Prędkość uderzenia o pas (klucz do oceny lądowania).GlideSlopeError – Odchylenie od ścieżki ILS.ElevatorPosition – Wychylenie steru wysokości (analiza techniki pilota).GPS (Lat/Lon/Hdg) – Pozycja geograficzna i kurs.Engine N1 (1 & 2) – Ciąg silników.Fuel – Pozostała ilość paliwa.OnGround – Flaga binarna (0/1) kontaktu z ziemią.4. Logika Aplikacji i Zapis DanychFormat: Pliki .CSV generowane z unikalną nazwą opartą na dacie i czasie.Kultura danych: Zastosowano FormattableString.Invariant, co wymusza kropkę jako separator dziesiętny (wymagane przez modele AI w Pythonie).Bezpieczeństwo: Implementacja OnRecvQuit zapewnia poprawne zamknięcie pliku CSV nawet przy nagłym wyłączeniu gry.5. Dziennik Postępów (Milestones)ZadanieStatusUwagiPołączenie SimConnect✅ GotoweStabilna komunikacja C# <-> MSFS.Eliminacja błędów DLL✅ GotoweNaprawiono błędy XAML i FileNotFound.Kalibracja zmiennych✅ GotoweNaprawiono przesunięcie danych (Weight/Flaps).Rozszerzony wektor✅ GotoweDodano Touchdown, GS Error i Engine N1.Zbieranie Datasetu🕒 W trakcieNależy nagrać loty: Ideal, Hard, Unstable.6. Wytyczne dla Agenta (Cursor AI / Future Chat)Kontekst: Pracujemy na net10.0-windows i architekturze x64.Zasada 1: Przy każdej modyfikacji TelemetryStruct, musisz zaktualizować AddToDataDefinition oraz nagłówek w csvWriter.WriteLine.Zasada 2: Pliki CSV są zapisywane w folderze Moje Dokumenty.Zasada 3: Pamiętaj, że PLANE PITCH DEGREES w MSFS ma wartości ujemne dla nosa w górę (nose-up).7. Plan na następną sesjęNagranie 3-5 lądowań o różnej jakości w MSFS.Eksport plików CSV do Google Colab.Napisanie skryptu w Pythonie do automatycznego wycinania momentu lądowania z długiego nagrania (Segmentation).Wizualizacja parametrów: Wykres wysokości względem progu pasa startowego.
