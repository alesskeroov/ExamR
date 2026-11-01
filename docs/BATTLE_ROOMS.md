# Qalıcı Battle otaqları

Otaq və üzvlük üçün əsas mənbə MSSQL-dir: `StoredBattleRooms` və `StoredBattleMembers`.
`AddPersistentBattleRooms` migration-ı mövcud cədvəlləri dəyişmədən bu iki cədvəli əlavə edir.

- Otaq statusları: `Waiting`, `InProgress`, `Completed`; bağlanma ayrıca `IsClosed` ilə saxlanır.
- Çıxış yalnız həmin SignalR bağlantısını ayırır. Hostun son bağlantısı ayrıldıqda qalan online iştirakçı avtomatik Host olur (bir neçə nəfər varsa ilk qoşulan seçilir). Digər tab açıqdırsa Host dəyişmir.
- Heç kim online qalmadıqda otaq və üzvlük qeydləri MSSQL-dən silinir; nəticə tarixçəsi qalır. “Çıx” üçün dərhal tətbiq edilir. Bağlantı itkisi / refresh üçün 15 saniyə gözlənilir; yenidən qoşulma əvvəlki yoxlamanı ləğv edir. Gözləyən dəvətlər qeyri-aktiv edilir.
- Otağın yaradıcısı, cari Host, ad, kod, PDF, üzvlər və oyunun bərpa snapshot-u DB-dədir. Snapshot sualları, cavabları, xalları, taymerləri, mute/kick və son 50 chat mesajını saxlayır. Bağlantı ID-ləri DB-yə yazılmır.
- Mövcud oyun dövrü bir serverdə işləyir. Restart zamanı aktiv oyun DB-dən bərpa olunur; cari sualın bitmə vaxtı və verilmiş cavablar dəyişmir, reveal yenidən ballanmır. Sonrakı suallar normal axınla davam edir.
- Host transferi otaq kilidi altında, `rowversion` yoxlaması və atomik DB yazması ilə edilir. Uğurlu yazmadan sonra bütün iştirakçılara `RoomUpdated` gəlir.
- Əl ilə bağlama yalnız cari Host üçündür; boş otağın avtomatik silinməsi ayrıca server qaydasıdır. Cleanup yalnız bitmiş lokal keşləri boşaldır.

## İnteqrasiya

| API / SignalR | Təyinat |
|---|---|
| `POST /api/battles` | Mövcud parametrlər + istəyə bağlı `name` (maks. 100 simvol) |
| `GET /api/battles/rooms?includeCompleted=false` | Özünün yaratdığı / üzv olduğu son 100 otaq; default aktiv otaqlar |
| `POST /api/battles/rooms/{code}/close` | Yalnız cari Host üçün bağlama |
| Hub `TransferHost(code, userId)` | Başqa aktiv üzvə Host ötürmək; özü / kənar istifadəçi / köhnə Host rədd edilir |
| Hub `CloseRoom(code)` | Cari Host üçün bağlama |
| `GET /api/friends/room/{code}?q=...` | Üzv üçün dostlar: ad/username axtarışı, real online status, otaqda/dəvət göndərilib işarələri |
| `POST /api/friends/{userId}/room-invite` | `{ "roomCode": "ABC123" }`; yalnız online və otaqda olmayan dost üçün |

Otaqdaxili dəvət zamanı dostluq, bloklar, online status, otağa üzvlük, Waiting statusu, limit və təkrar dəvət serverdə yoxlanır. Mövcud Friends səhifəsinin `/invite` axını oflayn bildiriş saxlama dəstəyini qoruyur. Dəvətin qəbulu və `JoinRoom` da aktual vəziyyəti yoxlayır; yer rezerv edilmir.

## Lokal yoxlama

1. `dotnet ef database update --project src/ExamFlow.Api`; API və Vite-i başladın.
2. Normal və private brauzer pəncərələrində iki hesaba daxil olun; onları dost edin.
3. A hesabı ad verilmiş otaq yaratsın, B qoşulsun. A-nın son tabı “Çıx” etsin: B Host olmalıdır. B də çıxanda otaq “Otaqlarım”dan və DB-dən silinməlidir. A-nın əlavə tabı açıqdırsa bir tabın çıxışı Hostu dəyişməməlidir.
4. Otaq daxilində “Dost dəvət et” açın. B oflayn olduqda sətir boz, düymə disabled olmalıdır. B daxil olduqda status yenilənməli, ad və username axtarışı işləməlidir.
5. B-yə dəvət göndərin: “Dəvət göndərildi” görünməlidir. B bildirişdən qəbul etdikdə otaq açılmalı, modalda “Otaqdadır” göstərilməlidir.
6. A, B-nin yanında “Host et” → təsdiq etsin. B-də Start/idarəetmə görünməli, A-da gizlənməlidir. B yarışı başlatsın.
7. 15 saniyə daxilində refresh / reconnect edin: Host, üzvlər, cavab və taymer qorunmalıdır. Heç kim qayıtmazsa otaq avtomatik silinməlidir. Restart zamanı da otağa 15 saniyəlik qoşulma müddəti verilir.
8. Otağı yalnız B bağlaya bilməlidir. Tamamlanmışları göstər seçimi nəticə statuslu kartları açır.

Avtomatik yoxlamalar: `dotnet build`, `dotnet test`; `client/` daxilində `npm run build`, `npx oxlint`.
