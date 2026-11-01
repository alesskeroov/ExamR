# Dostluq və Battle dəvətləri

## Lokal işə salma və yoxlama

```powershell
dotnet ef database update --project src/ExamFlow.Api
dotnet run --project src/ExamFlow.Api
# Başqa terminalda:
cd client
npm run dev
```

1. İki ayrı brauzer profili / normal və private pəncərə ilə iki hesaba daxil olun. Eyni brauzer profilində tablar `localStorage` girişini paylaşır.
2. `/friends` səhifəsində digər hesabın adını və ya username-ni axtarın. Hazırkı Identity modelində username giriş email-idir; axtarış nəticəsində email göstərilmir.
3. Sorğu göndərin. Digər hesabda zəngin sayğacı və “Gələn sorğular” yenilənməlidir. Sorğunu qəbul / rədd edin; hər iki hesabda siyahı yenilənir.
4. Dost siyahısındakı online statusu yoxlayın. Bir tabı bağlamaq kifayət deyil: istifadəçinin son açıq tabı bağlandıqda oflayn olur.
5. Battle lobby yaradın. Lobby-də “Dost dəvət et” modalından online dosta dəvət göndərin. Friends səhifəsindəki otaq seçimi axını da qalır. Dostda zəng → dəvət → “Battle-a qoşul” avtomatik `/battle/CODE` açır.
6. Dost oflayn ikən dəvət göndərin; yenidən daxil olduqda bildiriş görünür. Dəvəti rədd etməyi, vaxtı bitmiş / başlamış / bağlanmış / dolu otağı yoxlayın.
7. Dostu bloklayın: dostluq, gözləyən sorğular və aranızdakı dəvətlər ləğv edilir. Blok açıldıqda dostluq avtomatik bərpa edilmir.

Yoxlamalar: `dotnet build`, `dotnet test`, `client/` daxilində `npm run build` və `npx oxlint`.

## API və təhlükəsizlik

Bütün endpoint-lər JWT tələb edir. Sorğular / bildirişlər cari istifadəçinin ID-si ilə filtr edilir; başqasının resursu üçün 404 qaytarılır.

| Endpoint | Təyinat |
|---|---|
| `GET /api/friends` | Dostlar, gələn/göndərilən sorğular, istifadəçinin blokladığı hesablar |
| `GET /api/friends/search?q=...` | Ad / mövcud username, 2–100 simvol, maksimum 20 nəticə; özü və bloklanmış cütlər gizlənir |
| `POST /api/friends/{userId}/{action}` | `request`, `accept`, `reject`, `remove`, `block`, `unblock` |
| `GET /api/friends/lobbies` | Cari istifadəçinin host / iştirakçı olduğu açıq lobby-lər |
| `POST /api/friends/{userId}/invite` | `{ "roomCode": "ABC123" }`; yalnız qəbul edilmiş dostluq |
| `GET /api/notifications` | Son 100 bildiriş və bütün oxunmamışların sayı |
| `POST /api/notifications/read` | Özünün bütün bildirişlərini oxunmuş et |
| `POST /api/notifications/{id}/read` | Özünün bir bildirişini oxunmuş et |
| `POST /api/notifications/{id}/accept` | Öz Battle dəvətini qəbul et → `{ "code": "ABC123" }` |
| `POST /api/notifications/{id}/reject` | Öz Battle dəvətini rədd et |

Dostluq cütü DB-də unikal açarla saxlanır. Serializable tranzaksiyalar qarşılıqlı sorğu, təkrar dəvət və bloklama yarışlarını qoruyur; mövcud EF retry strategy istifadə olunur. Dostluq/dəvət yazmaları istifadəçi başına 10/dəqiqə ilə məhdudlaşdırılıb. Bildirişdəki dostluq sorğuları `friends/{actorId}/accept|reject` vasitəsilə cavablandırılır.

`Social:InvitationMinutes` (default 5, 1–60 dəqiqə) dəvətin ömrüdür. Qəbul zamanı dostluq, bloklar, vaxt, otaq statusu, tutum və kick vəziyyəti yenidən yoxlanır. Qəbul yer rezerv etmir: sonrakı `JoinRoom` da otağın aktual vəziyyətini yoxlayır.

## Saxlama və real-time

- `Friendships`: istifadəçi cütü, sorğunu göndərən, qəbul vəziyyəti və tarix.
- `UserBlocks`: bloklayan / bloklanan istifadəçi cütü.
- `SocialNotifications`: sahib, göndərən, növ, status, oxunma vaxtı və Battle snapshot-u (otaq kodu, host adı, PDF, sual sayı, bitmə vaxtı).
- Migration: `AddFriendsAndNotifications`; mövcud cədvəllər dəyişmir.
- `/hubs/social` mövcud SignalR/JWT infrastrukturunu istifadə edir. Qrup yalnız server tərəfindən autentifikasiya olunmuş istifadəçinin ID-si ilə seçilir. `SocialChanged` yalnız uyğun istifadəçilərə göndərilir; UI qorunan REST məlumatını yenidən alır.
- Presence bir serverin yaddaşındadır, hər istifadəçi üçün çoxsaylı bağlantı sayılır. Dostlar üçün görünür. Restart-da bağlantılar yenidən qurulur; dostluqlar, bildirişlər və qalıcı otaqlar DB-də saxlanılır. Otaqlar: `docs/BATTLE_ROOMS.md`.
- UI reconnect zamanı və ehtiyat olaraq periodik məlumat yeniləyir. Çox serverli presence/backplane bu versiyaya daxil deyil.
