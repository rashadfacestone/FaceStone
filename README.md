# Face Stone - Full Concrete Solutions
## C# ASP.NET Core RESTful Web API & SQL Server Backend

Official Web API repository for **Face Stone** (*FACESTONE - FULL CONCRETE SOLUTIONS*).
This backend has been completely transitioned to a modern, decoupled **RESTful Web API** with OpenAPI/Swagger documentation, JWT Bearer Token authentication, and SignalR real-time messaging.

---

### Database Connection

- **SQL Server Instance:** `DESKTOP-7S9HM7C\SQLEXPRESS01`
- **Database Name:** `facestone`
- **ORM:** Entity Framework Core 8.0 (SQL Server Provider)
- **Tables:** `[facestone].[dbo].[Users]`, `[facestone].[dbo].[ChatMessages]`, `[facestone].[dbo].[ChatAttachments]`

---

### Project Architecture & Structure

```
F:\FaceStoneWebsite\
├── FaceStone.sln                 # Visual Studio Solution File
├── FaceStone.Web.csproj           # ASP.NET Core Web API (.NET 8.0)
├── Program.cs                     # Startup, JWT, Swagger, SignalR, CORS & Pipeline
├── appsettings.json               # SQL Server Connection String & JWT Settings
├── run.bat                        # 1-Click Launcher (Starts API & opens Swagger UI)
├── Data/
│   └── FaceStoneDbContext.cs      # EF Core DbContext with Users, Messages, Attachments
├── Controllers/
│   ├── AuthController.cs          # /api/auth: Login, Register, Forgot Password, Profile
│   ├── ChatController.cs          # /api/chat: Real-time Messages, Attachments, Conversations
│   └── UsersController.cs         # /api/users: Directory & Profile retrieval
├── Models/
│   ├── UserAccount.cs             # Domain User Entity
│   ├── ChatMessage.cs             # Plain-text Chat Message Entity
│   ├── ChatAttachment.cs          # File Attachment Metadata Entity
│   └── DTOs/                      # Data Transfer Objects
│       ├── ApiResponse.cs         # Standardized API Envelope { success, message, data, errors }
│       ├── AuthDtos.cs            # Login, Register, Token, User payloads
│       └── ChatDtos.cs            # SendMessage, Conversation, Poll payloads
├── Services/
│   ├── IAuthService.cs            # Authentication Service Contract
│   └── AuthService.cs             # Salted Hashing & JWT Token Generation
├── Hubs/
│   └── ChatHub.cs                 # SignalR Real-Time WebSocket Hub (/chatHub)
└── wwwroot/
    └── uploads/chat/              # Uploaded document & image attachments
```

---

### Key API Features

1. **Interactive Swagger / OpenAPI UI**:
   - Access at `http://localhost:5124/swagger` or directly at root `http://localhost:5124`.
   - Supports **Authorize** button: enter `Bearer {token}` to test protected endpoints directly in the browser.
2. **JWT Bearer Token Authentication**:
   - Issue tokens with user claims (`nameid`, `name`, `email`, `role`, `username`, `phone`).
   - Standard 24-hour expiration (configurable in `appsettings.json`).
3. **SignalR WebSocket Real-Time Chat**:
   - Connect via `/chatHub?access_token={jwtToken}`.
   - Pushes live notifications (`OnNewMessage`, `OnAccountantReply`, `OnNewEmployeeMessage`).
4. **CORS Enabled**:
   - Ready for integration with any Single Page Application (React, Vue, Angular, Svelte) or mobile application (Flutter, React Native, Swift, Kotlin).

---

### API Endpoints Summary

#### Authentication (`/api/auth`)
| Method | Endpoint | Access | Description |
| :--- | :--- | :--- | :--- |
| `POST` | `/api/auth/login` | Anonymous | Authenticate with Email/Phone/Username & Password -> returns JWT token |
| `POST` | `/api/auth/register` | Anonymous | Register a new user account -> returns JWT token |
| `POST` | `/api/auth/forgot-password` | Anonymous | Dispatch password reset request |
| `POST` | `/api/auth/detect-identifier` | Anonymous | Auto-detect identifier format (`email`, `phone`, `username`) |
| `GET` | `/api/auth/me` | `Bearer Token` | Retrieve current authenticated user profile |

#### Real-Time Chat (`/api/chat`)
| Method | Endpoint | Access | Description |
| :--- | :--- | :--- | :--- |
| `POST` | `/api/chat/send` | `Bearer Token` | Send message with optional file attachment (`multipart/form-data`) |
| `GET` | `/api/chat/conversations` | `Accountant, CEO` | Retrieve all employee threads with unread counters |
| `GET` | `/api/chat/conversations/{id}`| `Accountant, CEO` | Retrieve conversation messages for specific employee and mark as read |
| `GET` | `/api/chat/history` | `Bearer Token` | Retrieve employee's conversation history with Accounting Desk |
| `GET` | `/api/chat/poll` | `Bearer Token` | Polling fallback for clients without WebSocket support |

#### Users (`/api/users`)
| Method | Endpoint | Access | Description |
| :--- | :--- | :--- | :--- |
| `GET` | `/api/users` | `Accountant, CEO, IT` | List registered users with optional `role` and `search` filters |
| `GET` | `/api/users/{id}` | `Accountant, CEO, IT` | Retrieve single user record by GUID |

---

### How to Run

#### Option 1: Double-Click `run.bat`
Double-click `F:\FaceStoneWebsite\run.bat`. It will launch the Web API on `http://localhost:5124` and automatically open the **Swagger UI** in your browser.

#### Option 2: Command Line
```powershell
cd "F:\FaceStoneWebsite"
dotnet run --project FaceStone.Web.csproj --urls "http://localhost:5124"
```

---

### Test Accounts in SQL Server

| User | Email | Username | Phone | Password | Role |
| :--- | :--- | :--- | :--- | :--- | :--- |
| **Alexander Wright** | `alex@facestone.com` | `alex_wright` | `+15550192834` | `Password123!` | `Employee` |
| **Sarah Mitchell** | `sarah@facestone.com` | `sarah_concrete` | `+1555-018-9999` | `SecretPass123!` | `Accountant` |
