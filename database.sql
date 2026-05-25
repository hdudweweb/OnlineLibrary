create database bibleoteka04

use bibleoteka04

create table Age(
AgeId int primary key identity(1,1),
AgeName nvarchar(3)
)

create table Chapters(
ChaptersId int primary key identity (1,1),
NameChap nvarchar(50)
)

create table Author(
AuthorId int primary key identity(1,1),
LastName nvarchar(50),
FirstName nvarchar(50),
MidleName nvarchar(50)
)

create table Book(
BookId int primary key identity(1,1),
[Name] nvarchar(100),
TotalPages int,
PublicationYear int,
ISBN nvarchar(20),
DescriptionBook nvarchar(max),
BookFile VARBINARY(MAX) NULL,
FileName NVARCHAR(255) NULL,
ContentType NVARCHAR(100) NULL,
AgeId int foreign key references Age(AgeId),
ChapterId int foreign key references Chapters(ChaptersId),
AuthorId int foreign key references Author(AuthorId)
)

create table Roles(
RolesId int primary key identity(1,1),
RoleName nvarchar(50)
)

create table Users(
UsersId int primary key identity(1,1),
Username nvarchar(50),
Created datetime,
Email nvarchar(50),
RolesId int foreign key references Roles(RolesId),

)
CREATE TABLE Favorites (
FavoritesId  INT PRIMARY KEY  IDENTITY(1,1),
UserId INT NOT NULL FOREIGN KEY REFERENCES Users(UsersId),
BookId INT NOT NULL FOREIGN KEY REFERENCES Book(BookId),
AddedAt DATETIME2 NOT NULL DEFAULT SYSUTCDATETIME(),
CONSTRAINT UQ_Favorites UNIQUE(UserId, BookId)
);


create table AuthUsers(
AuthUsersId int primary key foreign key references Users(UsersId),
Password nvarchar(max),
Email nvarchar(50)
)

create table Covers(
CoversId int primary key identity(1,1),
Cover varbinary(max),
BookId int foreign key references Book(BookId)
)

create table Languages(
LanguagesId int primary key identity(1,1),
NameLang nvarchar(15),
BookId int foreign key references Book(BookId)
)

create table PublishingHouse(
PublishingHouse int primary key identity(1,1),
NamePublish nvarchar(100),
BookId int foreign key references Book(BookId)
)


create table Genre(
GenreId int primary key identity(1,1),
GenreName nvarchar(50)
)

create table GenreBook(
GenreBookId int primary key identity(1,1),
BookId int foreign key references Book(BookId),
GenreId int foreign key references Genre(GenreId)
)
CREATE UNIQUE INDEX UX_Author_FIO
ON Author(LastName, FirstName, MidleName);

INSERT INTO Roles (RoleName) VALUES
(N'Администратор'), (N'Читатель');
INSERT INTO Users (Username, Created, Email, RolesId) VALUES
(N'AdminUser', GETDATE(), N'admin@example.com', 1 ),
(N'Reader123', GETDATE(), N'reader1@example.com', 2);
INSERT INTO AuthUsers (AuthUsersId, [Password], Email) VALUES
(1, HASHBYTES('SHA2_256', N'adminpass123'), N'admin@example.com'),
(2, HASHBYTES('SHA2_256', N'readerpass456'), N'reader1@example.com');
INSERT INTO Age (AgeName) VALUES
(N'0+'), (N'6+'), (N'12+'), (N'16+'), (N'18+');
INSERT INTO Genre (GenreName) VALUES
(N'Фантастика'),(N'Фэнтези'),(N'Детектив'),(N'Приключения'),(N'Исторический роман'),
(N'Романтика'),(N'Триллер'),(N'Ужасы'),(N'Научная литература'),(N'Биография'),
(N'Психология'),(N'Поэзия'),(N'Драма'),(N'Комедия'),(N'Эссе'),(N'Мифология'),
(N'Классическая литература'),(N'Современная проза'),(N'Детская литература'),(N'Юмор');
INSERT INTO Author (LastName, FirstName, MidleName) VALUES
(N'Пушкин', N'Александр', N'Сергеевич'),
(N'Толстой', N'Лев', N'Николаевич'),
(N'Достоевский', N'Фёдор', N'Михайлович'),
(N'Гоголь', N'Николай', N'Васильевич'),
(N'Чехов', N'Антон', N'Павлович'),
(N'Булгаков', N'Михаил', N'Афанасьевич'),
(N'Есенин', N'Сергей', N'Александрович'),
(N'Ахматова', N'Анна', N'Андреевна'),
(N'Оруэлл', N'Джордж', NULL),
(N'Роулинг', N'Джоан', NULL),
(N'Толкин', N'Джон', N'Рональд Руел'),
(N'Кинг', N'Стивен', NULL),
(N'Мартин', N'Джордж', N'Р. Р.'),
(N'Лем', N'Станислав', NULL),
(N'Хемингуэй', N'Эрнест', NULL);


INSERT INTO Languages (NameLang, BookId) VALUES
(N'Русский', NULL),
(N'Английский', NULL),
(N'Немецкий', NULL),
(N'Французский', NULL),
(N'Испанский', NULL),
(N'Итальянский', NULL),
(N'Польский', NULL),
(N'Украинский', NULL),
(N'Белорусский', NULL),
(N'Китайский', NULL),
(N'Японский', NULL),
(N'Корейский', NULL),
(N'Турецкий', NULL),
(N'Арабский', NULL),
(N'Португальский', NULL);
ALTER TABLE Favorites
ADD Status NVARCHAR(20) NOT NULL
CONSTRAINT DF_Favorites_Status DEFAULT N'Добавить';

ALTER TABLE Favorites
ADD CONSTRAINT CK_Favorites_Status
CHECK (Status IN (N'Читаю', N'В планах', N'Прочитано',N'Добавить '));


IF COL_LENGTH('Users', 'Avatar') IS NULL
BEGIN
    ALTER TABLE Users ADD Avatar VARBINARY(MAX) NULL;
END
GO


IF EXISTS (
    SELECT 1
    FROM sys.default_constraints
    WHERE parent_object_id = OBJECT_ID('Favorites')
      AND name = 'DF_Favorites_Status'
)
BEGIN
    ALTER TABLE Favorites DROP CONSTRAINT DF_Favorites_Status;
END
GO

IF EXISTS (
    SELECT 1
    FROM sys.check_constraints
    WHERE parent_object_id = OBJECT_ID('Favorites')
      AND name = 'CK_Favorites_Status'
)
BEGIN
    ALTER TABLE Favorites DROP CONSTRAINT CK_Favorites_Status;
END
GO

ALTER TABLE Favorites
ADD CONSTRAINT DF_Favorites_Status
DEFAULT N'В планах' FOR Status;
GO

ALTER TABLE Favorites
ADD CONSTRAINT CK_Favorites_Status
CHECK (Status IN (N'Читаю', N'В планах', N'Прочитано', N'В избранном'));
