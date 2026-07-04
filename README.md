# MyArchive

MyArchive e uma aplicacao pessoal de gerenciamento de acervo.

O objetivo do sistema e permitir catalogar, acompanhar, avaliar e registrar diferentes tipos de conteudo consumido pelo usuario, com uma experiencia visual de biblioteca pessoal digital, e nao de gerenciador de tarefas.

## Visao do produto

O sistema deve organizar conteudos como:

- Livros
- Mangas
- HQs
- Games
- Filmes
- Series
- Series animadas
- Animes

Todos os itens compartilham uma base comum de informacoes, mas cada categoria pode possuir propriedades e regras especificas.

## Conceito central

Tudo no sistema e um item de acervo.

Cada item representa uma obra ou uma versao especifica de uma obra, como no caso de games em plataformas diferentes.

Exemplos:

- `Skyrim (PS3)`
- `Skyrim (PS4)`
- `Skyrim (PC)`

## Entidade base

Todo item cadastrado possui:

### Informacoes gerais

- `Id`
- `Titulo`
- `Imagem de capa`
- `Data de criacao`
- `Data de atualizacao`

### Avaliacao

- `Nota`
  - decimal
  - escala de `0` a `10`
  - ate `2` casas decimais

### Resenha

- texto livre
- limite aproximado de `200` linhas

### Observacoes

- campo livre para anotacoes adicionais

## Exibicao visual

Todos os itens devem exibir:

- capa em destaque
- titulo
- status colorido
- nota, quando existir

A imagem de capa e obrigatoria para todas as categorias.

O foco visual da aplicacao deve lembrar uma biblioteca pessoal, com destaque forte para capas e identificacao rapida por status.

## Categorias

### Livros

Status:

- Nao lido
- Lendo
- Lido

### Mangas

Mesma estrutura de livros.

Status:

- Nao lido
- Lendo
- Lido

### HQs

Mesma estrutura de livros.

Status:

- Nao lido
- Lendo
- Lido

### Games

Campos adicionais:

- `Midia`
  - Fisica
  - Digital

- `Plataforma`
  - PS1
  - PS2
  - PS3
  - PS4
  - PS5
  - Xbox 360
  - Xbox One
  - Xbox Series
  - Nintendo Switch
  - PC
  - Outros

Status:

- Nao finalizado
- Jogando
- Finalizado
- Gratuito
- Outro dono
- Nao zeravel

Regras relevantes:

- o mesmo jogo pode ser cadastrado mais de uma vez em plataformas diferentes
- cada versao e um item proprio
- deve existir uma marcacao manual e opcional de `Finalizado em outra plataforma`

### Filmes

Status:

- Nao visto
- Vendo
- Assistido

### Series

Status:

- Nao vista
- Vendo
- Assistida

Estrutura interna:

- temporadas
- episodios

Cada temporada possui:

- numero
- titulo opcional
- status automatico

Cada episodio possui:

- numero
- titulo opcional
- assistido
- nota opcional
- resenha opcional

Criacao automatica:

- o usuario informa a temporada
- informa a quantidade de episodios
- o sistema gera os episodios automaticamente

Regras automaticas:

- quando todos os episodios de uma temporada forem assistidos, a temporada passa a `Assistida`
- quando todas as temporadas forem assistidas, a serie passa a `Assistida`

### Series animadas

Mesma estrutura de series.

### Animes

Mesma estrutura de series.

## Sistema de cores

Todos os status devem possuir cores proprias.

Exemplos:

- `Lido = Verde`
- `Lendo = Azul`
- `Nao lido = Cinza`

No futuro, essas cores devem se tornar configuraveis.

## Dashboard

O dashboard deve apresentar:

- quantidade total por categoria
- conteudo em andamento
- conteudo concluido
- conteudo nao iniciado
- media de notas
- ultimos itens adicionados

## Direcao de design

O sistema nao deve parecer um checklist ou um gerenciador de tarefas.

Ele deve se parecer com:

- uma biblioteca pessoal de livros
- um catalogo de filmes e series
- uma colecao de games com capas e status

Referencias de experiencia:

- Goodreads
- Letterboxd
- MyAnimeList
- biblioteca de jogos

## Direcao tecnica atual

Arquitetura prevista:

- Frontend: Blazor WebAssembly
- Backend: ASP.NET Core Web API
- Banco de dados: PostgreSQL

## Como rodar

### Pre-requisitos

- `.NET 9 SDK`
- `PostgreSQL`
- acesso ao `NuGet` para restaurar os pacotes na primeira execucao

### Configurar o banco

A API usa PostgreSQL via `Npgsql`. Para conectar no Supabase, configure a connection string fora do repositorio usando `dotnet user-secrets` ou variavel de ambiente.

Formato para conexao direta do Supabase:

```text
Host=db.mbrvqhicqtyqkoiwphby.supabase.co;Port=5432;Database=postgres;Username=postgres;Password=YOUR_SUPABASE_DATABASE_PASSWORD;SSL Mode=Require;Trust Server Certificate=true
```

Configurar com `dotnet user-secrets`:

```powershell
dotnet user-secrets set "ConnectionStrings:MyArchive" "Host=db.mbrvqhicqtyqkoiwphby.supabase.co;Port=5432;Database=postgres;Username=postgres;Password=YOUR_SUPABASE_DATABASE_PASSWORD;SSL Mode=Require;Trust Server Certificate=true" --project .\MyArchive\MyArchive.csproj
```

Ou configurar por variavel de ambiente:

```powershell
$env:ConnectionStrings__MyArchive="Host=db.mbrvqhicqtyqkoiwphby.supabase.co;Port=5432;Database=postgres;Username=postgres;Password=YOUR_SUPABASE_DATABASE_PASSWORD;SSL Mode=Require;Trust Server Certificate=true"
```

O arquivo `.env.example` mostra o nome da variavel esperada, mas o `.env` real deve continuar fora do Git.

Para desenvolvimento local sem Supabase, a API ainda possui uma configuracao padrao em `MyArchive/appsettings.Development.json`:

- `Host=localhost`
- `Port=5432`
- `Database=myarchive_dev`
- `Username=postgres`
- `Password=postgres`

Subir o banco local opcional:

```powershell
docker compose up -d postgres
```

Parar o container local:

```powershell
docker compose down
```

Remover tambem o volume de dados local:

```powershell
docker compose down -v
```

### Projeto `MyArchive`

Esse e o backend em `ASP.NET Core Web API`.

Rodar a API a partir da raiz do repositorio:

```powershell
dotnet run --project .\MyArchive\MyArchive.csproj
```

URLs locais configuradas:

- `https://localhost:7107`
- `http://localhost:5107`

Observacoes:

- a API aplica `EnsureCreated` na inicializacao e cria a base se ela ainda nao existir
- se o PostgreSQL/Supabase nao estiver acessivel, a API vai falhar na inicializacao
- o CORS de desenvolvimento ja aceita o frontend em `https://localhost:7108` e `http://localhost:5108`

### Projeto `MyArchive.Client`

Esse e o frontend em `Blazor WebAssembly`.

Rodar o cliente a partir da raiz do repositorio:

```powershell
dotnet run --project .\MyArchive.Client\MyArchive.Client.csproj
```

URLs locais configuradas:

- `https://localhost:7108`
- `http://localhost:5108`

Configuracao importante:

- em desenvolvimento, `MyArchive.Client/wwwroot/appsettings.Development.json` aponta para `http://localhost:5107`
- portanto, a API precisa estar em execucao junto com o cliente

### Projeto `MyArchive.Shared`

Esse projeto contem contratos e modelos compartilhados entre frontend e backend.

Ele nao e um projeto executavel e nao deve ser iniciado com `dotnet run`.

### Rodar tudo em desenvolvimento

Abra dois terminais na raiz do repositorio.

Terminal 1:

```powershell
dotnet run --project .\MyArchive\MyArchive.csproj
```

Terminal 2:

```powershell
dotnet run --project .\MyArchive.Client\MyArchive.Client.csproj
```

Fluxo esperado:

1. rode `docker compose up -d postgres`
2. inicie a API
3. inicie o frontend
4. abra `https://localhost:7108`

### Rodar pela solution

Se quiser apenas restaurar e compilar tudo:

```powershell
dotnet restore .\MyArchive.sln
dotnet build .\MyArchive.sln
```

## Escopo sugerido para a primeira versao

Para evitar que o projeto nasca grande demais, a primeira versao deve priorizar o nucleo do produto.

### MVP recomendado

- cadastro de categorias principais
- item base com capa obrigatoria
- status por categoria
- nota
- resenha
- observacoes
- dashboard simples
- listagem visual em formato de biblioteca
- games com plataforma e midia

### Fase seguinte

- temporadas e episodios para series, series animadas e animes
- criacao automatica em lote de episodios
- regras automaticas de conclusao por temporada e serie
- configuracao futura de cores
- relacionamento futuro entre versoes do mesmo game

## Principio de produto

O sistema esta ficando mais interessante justamente porque deixa de ser um simples checklist e passa a funcionar como um acervo pessoal curado pelo usuario.

O cuidado principal agora e manter coesao:

- primeiro, uma base forte de catalogo
- depois, regras especificas por categoria
- por fim, automatizacoes mais profundas

Esse caminho reduz retrabalho e ajuda o projeto a chegar a uma primeira versao realmente utilizavel.



