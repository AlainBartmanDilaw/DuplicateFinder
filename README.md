# DuplicateFinder

Application Windows Forms (.NET 10) pour détecter les fichiers en double via SHA256.

## Prérequis

- Windows 10/11
- [.NET 10 SDK](https://dotnet.microsoft.com/download/dotnet/10.0)
- Visual Studio 2022+ ou `dotnet CLI`

## Build & Run

```bash
cd DuplicateFinder
dotnet restore
dotnet run
```

Ou avec Visual Studio : ouvrir `DuplicateFinder.csproj` → **F5**.

## Architecture

```
DuplicateFinder/
├── Models/
│   ├── CrcEntry.cs          # Table Crc (SHA256 unique)
│   ├── FileEntry.cs         # Table Fichier (chemin, taille, FK vers Crc)
│   └── DuplicateGroup.cs    # Vue métier : groupe de doublons
├── Data/
│   └── FileRepository.cs    # LiteDB — CRUD + requête doublons
├── Services/
│   ├── DirectoryScanner.cs  # Scan récursif + calcul SHA256
│   └── RecycleBinService.cs # Envoi corbeille Windows (SHFileOperation)
├── UI/
│   ├── MainForm.cs          # Fenêtre principale
│   ├── ScanForm.cs          # Dialogue configuration scan
│   ├── ProgressForm.cs      # Progression du scan
│   ├── ImageComparePanel.cs # Affichage côte-à-côte de 2 images
│   └── FileListForm.cs      # Liste complète des fichiers
└── Program.cs
```

## Base de données

Fichier LiteDB stocké dans :  
`%AppData%\DuplicateFinder\store.db`

### Tables

| Table   | Colonnes |
|---------|----------|
| `Crc`   | `Id` (Guid PK), `Sha256` (unique) |
| `Fichier` | `Id` (Guid PK), `FullPath`, `FileSize`, `CrcId` (FK) |

## Fonctionnement

1. **Scanner** → choisir répertoires + filtres d'extension  
2. Le scanner calcule le **SHA256** de chaque fichier  
3. Les données sont stockées dans LiteDB  
4. L'écran principal affiche les **doublons 2 à 2** (référent vs doublon)  
5. Navigation par groupes et par paires  
6. Bouton **🗑 Corbeille** pour envoyer un fichier dans la corbeille Windows  

## Dépendances NuGet

- [`LiteDB`](https://www.litedb.org/) 5.x — base NoSQL embarquée en fichier unique
