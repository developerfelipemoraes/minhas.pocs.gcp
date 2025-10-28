# API de Upload de Alta Performance para Google Cloud Storage

## Visão Geral

Esta API, desenvolvida em .NET 6, oferece uma solução robusta e altamente escalável para o upload de arquivos para o Google Cloud Storage (GCS). A arquitetura foi projetada para maximizar a performance e a eficiência, utilizando um fluxo de upload direto via *streaming* com URLs Assinadas V4, o que minimiza a carga no servidor da API e otimiza a velocidade de transferência.

## Arquitetura da Solução

O fluxo de upload foi desenhado para ser o mais direto e eficiente possível, evitando o armazenamento intermediário de arquivos no servidor da API.

```mermaid
graph TD
    A[Cliente] -->|1. Requisição POST com Stream do Arquivo| B(API Endpoint: /upload/{bucket}/{objeto});
    B -->|2. Encaminha o Stream| C{GcsUploaderService};
    C -->|3. Gera URL Assinada V4| D[Google Cloud Storage];
    D -->|4. Retorna URL Assinada| C;
    C -->|5. Requisição PUT com Stream| E(URL Assinada do GCS);
    E -->|6. Upload do Arquivo Concluído| D;
    C -->|7. Retorna Duração do Upload| B;
    B -->|8. Retorna 200 OK com a Duração| A;
```

### Detalhamento do Fluxo

1.  **Cliente para API Endpoint**: O cliente inicia uma requisição `POST` para o endpoint `/upload/{bucket}/{objeto}`, enviando o conteúdo do arquivo como um *stream* no corpo da requisição.
2.  **API Endpoint para o Serviço**: O endpoint da API recebe a requisição e encaminha o *stream* diretamente para o `GcsUploaderService`.
3.  **Serviço para o GCS (Autenticação)**: O `GcsUploaderService` se comunica com o Google Cloud Storage para gerar uma URL Assinada V4. Esta requisição é autenticada utilizando as *Application Default Credentials* (ADC) do ambiente, garantindo segurança e flexibilidade.
4.  **GCS para o Serviço**: O GCS retorna uma URL única e com tempo de expiração limitado para o serviço.
5.  **Serviço para o GCS (Upload)**: O `GcsUploaderService` então realiza uma requisição `PUT` para a URL assinada, enviando o *stream* do arquivo diretamente para o GCS.
6.  **Upload Concluído**: O GCS recebe o *stream* e salva o arquivo no *bucket* especificado.
7.  **Serviço para a API Endpoint**: Após a conclusão do upload, o `GcsUploaderService` retorna o tempo total do upload para o endpoint.
8.  **API Endpoint para o Cliente**: O endpoint retorna uma resposta `200 OK` para o cliente, contendo a duração do upload.

## Performance, Velocidade e Escalabilidade

Esta arquitetura foi escolhida especificamente para otimizar a performance e a escalabilidade da solução. Abaixo estão os principais benefícios:

### 1. **Mínimo Consumo de Memória e CPU na API**

-   **Streaming Direto**: O arquivo é transmitido (*streamed*) diretamente do cliente para o GCS, passando pela API sem ser armazenado em memória ou em disco. Isso significa que a API atua como um *proxy* leve, consumindo uma quantidade mínima de recursos.
-   **Suporte a Arquivos Grandes**: Como o arquivo não é carregado na memória, a solução suporta o upload de arquivos de grande volume (Gigabytes ou mais) sem impactar a performance do servidor da API.

### 2. **Alta Velocidade de Upload**

-   **Upload Direto para o GCS**: A transferência de dados ocorre diretamente para a infraestrutura global do Google, que é otimizada para alta velocidade e baixa latência.
-   **Menos Saltos de Rede**: A arquitetura de URL assinada elimina a necessidade de um "salto" intermediário (onde o arquivo seria primeiro salvo no servidor da API e depois enviado para o GCS), reduzindo o tempo total de upload.

### 3. **Escalabilidade Horizontal**

-   **Servidores de API Leves (*Stateless*)**: Como os servidores da API não armazenam estado nem dados de arquivos, eles são extremamente leves e podem ser escalados horizontalmente com facilidade para lidar com um grande número de requisições simultâneas.
-   **Balanceamento de Carga Eficiente**: A carga principal (a transferência de dados) é gerenciada pelo GCS, permitindo que um balanceador de carga distribua as requisições de upload de forma eficiente entre as instâncias da API.

### 4. **Segurança Aprimorada**

-   **URLs Temporárias e Seguras**: As URLs assinadas são de curta duração e concedem permissões limitadas (apenas `PUT` para um objeto específico), reduzindo a superfície de ataque.
-   **Autenticação Centralizada**: O uso de *Application Default Credentials* (ADC) simplifica a gestão de credenciais e se integra facilmente com os mecanismos de segurança do Google Cloud, como o *Workload Identity* em GKE.

## Como Executar

### Pré-requisitos

-   [.NET 6 SDK](https://dotnet.microsoft.com/download/dotnet/6.0)
-   Credenciais do Google Cloud configuradas (via `gcloud auth application-default login` para desenvolvimento local).

## Endpoints da API

### Upload

A API oferece dois endpoints para upload, cada um otimizado para um caso de uso específico.

#### 1. Upload via Raw Stream

-   **Endpoint**: `POST /upload/stream/{bucketName}/{objectName}`
-   **Descrição**: Este é o método de **mais alta performance**. Ele espera que o corpo da requisição (`Request.Body`) seja o *stream* bruto do arquivo.
-   **Caso de Uso**: Ideal para clientes (como aplicativos móveis, desktops ou outros serviços de backend) que podem enviar o arquivo diretamente no corpo da requisição, sem encapsulamento de formulário.
-   **Exemplo com `curl`**:
    ```sh
    curl -X POST --data-binary "@/caminho/para/seu/arquivo.jpg" \
      -H "Content-Type: image/jpeg" \
      http://localhost:5000/upload/stream/seu-bucket/nome-do-arquivo.jpg
    ```

#### 2. Upload via Formulário (`IFormFile`)

-   **Endpoint**: `POST /upload/form/{bucketName}`
-   **Descrição**: Este endpoint utiliza o padrão `multipart/form-data`, o que o torna ideal para uploads a partir de formulários web. Ele aceita um parâmetro `IFormFile`. A implementação garante alta performance ao ler o arquivo como um stream, sem bufferizá-lo por completo em memória.
-   **Caso de Uso**: Perfeito para aplicações web (React, Angular, Blazor, etc.) que enviam arquivos através de um `<input type="file">`.
-   **Exemplo com `curl`**:
    ```sh
    curl -X POST -F "file=@/caminho/para/seu/arquivo.jpg" \
      http://localhost:5000/upload/form/seu-bucket
    ```

### Download

#### Download Direto via Stream

-   **Endpoint**: `GET /download/{bucketName}/{objectName}`
-   **Descrição**: Realiza o download de um arquivo do GCS, transmitindo-o diretamente para o cliente. Esta abordagem é altamente performática e ideal para servir arquivos de qualquer tamanho.
-   **Caso de Uso**: Servir arquivos para download em uma aplicação web ou para outros serviços.

## Como Executar

### Pré-requisitos

-   [.NET 6 SDK](https://dotnet.microsoft.com/download/dotnet/6.0)
-   Credenciais do Google Cloud configuradas (via `gcloud auth application-default login` para desenvolvimento local).

### Executando a API

1.  Navegue até o diretório `src/SignedUrlGcs.Api`.
2.  Execute `dotnet run`.
3.  A API estará disponível em `http://localhost:5000` (ou similar).

### Executando o Teste de Performance

1.  Navegue até o diretório `src/TestUploader`.
2.  Atualize a constante `BucketName` no arquivo `Program.cs` com o nome do seu *bucket* no GCS.
3.  Execute `dotnet run` para iniciar o benchmark de upload.
