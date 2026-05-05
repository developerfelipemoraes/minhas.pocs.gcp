
Felipe Moraes Aneas
12:45 (há 0 minuto)
para mim

# lADR - [US] - [Titulo do documento] - Solução de Arquitetura  (obrigatório)

## Arquivo de Decisão de Arquitetura (ADR)

### Título: [Titulo do documento] (obrigatório)

**Data**: [data atual]  **(obrigatório)**
**Autores**: [usuários reunião]   **(obrigatório)**

## Features (obrigatório)

Avaliação de implementação de integração  

[Solution Design - [US] - [Titulo do Card]](https://dev.azure.com/CP-TI/Agile%20Delivery/_workitems/edit/[])

## Classificação de Domínio e Subdomínio   **(obrigatório)**

| Classificação  | Domínio e Subdomínio         |
| ------------   | -----------                        |
| Empresa        | Care Plus |
| Domínio        | Sinistro |
| Subdomínio     | Autorização |
| Departamento   | N/A                          |

## Contexto  (obrigatório)

O processo de validação das informações será realizado pelo sistema [.........]
[.........]

### Opções Consideradas (opcional)

- Automatizar as consultas atuais por meio da  API , integrada ao Worker .
- [.........]

## Decisão (obrigatório)

Em uma conversa com a equipe, avançamos com a solução proposta, levando em consideração as restrições de integração e recursos disponíveis [.........]
[.........]

## Arquitetura de Sistemas

### Definições e padrões  (opcional)


> **Neste item, incluímos apenas os itens que vão compor a solução.**


| **Sigla**               | **Descrição**                                                |
| ----------------------- | ------------------------------------------------------------ |
| **Web  -**  **SPA**     | Aplicação  Angular **SPA(Single Page Application)**,  onde todas as funcionalidades de front-end  estão concentradas em uma única página. |
| **Apigee- Proxy **      | O Apigee  é utilizado para integrar aplicações externas que precisam acessar APIs  internas da CarePlus,  ou seja, quando necessário acesso da internet para intranet. |
| **Api**  **BFF**        | A  API BFF funciona como Sistema Especialista, gerenciando a integração com o  front-end  e as regras específicas do sistema. Ela coordena múltiplas APIs Core de  domínio e facilita a comunicação com parceiros externos usando o Apigee  como proxy. |
| **Api  Core**           | A  API Core centraliza o domínio e subdomínio, gerenciando as principais regras  de negócio. |
| **Api  Serviço**        | A  API de serviço oferece funcionalidades as **APIs Core** e **APIs BFF**, Utilizar  serviço com transferência de arquivos via **FTP, geração de PDFs,  e envio de notificações como e-mail e SMS**. Através da implementação do  pacote **CarePlus.Infra.Service.Client** |
| **Worker** **Services** | Os  **Workers Services** são aplicações do Windows utilizadas para processamento em  segundo plano (background). Eles realizam integrações internas e externas,  conectando-se às APIs Core, APIs externas e bancos de dados. |



### Glossário API  (opcional)

| Sigla         | Descrição                                                    |
| :------------ | :----------------------------------------------------------- |
| Mobile App    | Aplicativo do beneficiário da Careplus.                      |
| API Core      | Api Principal de domínio e subdomínio, ela e responsável por gerenciar os principais regras de negócio do domínio principal |
| Api Externa   | Api disponibilizada pela Ommi.                               |
| Worke Sevices | Serviço de integração para enviar dos dados de beneficiário para a Api Omni - Credito Farmácia. |


### Diagrama de Integração

####  Integração entre os sistemas (opcional)

| ![image-20230919103244808](C:\Users\rvsousa\Care Plus\Arquitetura de Referência - General\Documentação Tecnica\00 - Template\.assets\Estruturas-Integracao-Sistemas-Interno-Externo.png) |
| :----------------------------------------------------------: |
|                    *Figura: integração *                     |

### Sistemas Externos (opcional)

No contexto da arquitetura dos sistemas externos.

- SAFEE - Integração Externo
- [......]

### Proxy Apigee (opcional)

O proxy do Apigee desempenha um papel crucial ao atuar como intermediário na comunicação entre as APIs da [...........] e a API da Care Plus. Este mecanismo não apenas simplifica a integração entre diferentes sistemas, mas também melhora significativamente os padrões de segurança e eficiência.

#### Padrão de Protocolo: HTTPS e APIs REST

A implementação do proxy utiliza o protocolo HTTPS em conjunto com APIs REST para garantir a segurança e a confiabilidade na troca de dados entre os sistemas.

#### Segurança e Confiabilidade

- **Autenticação e Autorização**: O proxy implementa protocolos de autenticação robustos para garantir que apenas sistemas autorizados possam acessar as APIs. Isso é essencial para proteger os dados sensíveis durante o trânsito.
- **Chaves de Acesso**: O processo de autenticação envolve o uso de duas chaves, `Key` e `Secret`, que devem ser incluídas no cabeçalho das requisições HTTP. Essas chaves são fornecidas pela Care Plus e são obrigatórias para todas as chamadas às APIs.
- **Cadastro de Origem**: O domínio do cliente precisa ser cadastrado e disponibilizado como parte do processo de autenticação. Isso assegura que apenas chamadas de origens reconhecidas sejam permitidas.

### API BFF e API Core Care Plus (opcional)

- A API BFF é responsável por recepcionar as informações de solicitação da [......], controlar a transação dessas informações, enviar a solicitação para a [......]..., receber o retorno das informações e registrar no banco de dados. As integrações a serem realizadas, incluindo o registro de status e controle, devem ser feitas via API Core de .
- A API BFF disponibilizará end-points para que possam ser realizadas as transações de solicitação e consulta da solicitação.
- [......]

### Worker Care Plus (opcional)

- [Nome do worker]
- Git: [Nome repositório Git] 
  - Informações de configuração do Woker enviada via e-mail

- Realizará a ação de consultar o status do CNPJ quando houver agendamento disponível. Este agendamento será configurado no portal do sistema de Risco e Compliance.
- [....]

### Database (Repositórios) (opcional)

- Criar um repositório(Tabela)  único para armazenar os dados , com o retorno da consulta realizada na api. 
- [....]

## Consequências (obrigatório)

### Prós 

#### Proxy Apigee:

- Autenticação para garantir a comunicação entre as api 
- [......]

#### API BFF e API Core:

- Controle centralizado das transações de informações.
- [....]

#### Disponibilização de End-points pela API BFF:

- Flexibilidade para realizar transações de solicitação e consulta.
- [....]

#### Worker Care Plus:

- Automatização da consulta de status do CNPJ com base em agendamentos.
- [....]

#### Database (Repositórios):

- [....]

### Contras 

#### API BFF e API Core:

- Complexidade na implementação e manutenção das APIs.
- [....]

#### Disponibilização de Endpoints pela API BFF:

- Segurança e controle de acesso devem ser rigorosamente gerenciados.

- [....]

#### Worker Care Plus:

- Dependência de agendamentos pode para futuras consultas de status.
- [....]

#### Database (Repositórios):

- [....]

## Padrões de Arquitetura de Referência (obrigatório)

Os artigos a seguir fornecem uma análise detalhada das arquiteturas desenvolvidas e recomendadas.

| Arquitetura                                                  | Tipo   | Versão |
| :----------------------------------------------------------- | :----- | :----- |
| [Especificação Api Rename projetos](https://careplusmedicina.sharepoint.com/sites/CarePlusLabs/SitePages/arquitetura-referencia/web-api/Especifica%C3%A7%C3%A3o-Api-Rename-projetos.aspx) | Artigo | 0.0.1  |
| [Especificação API - RESTful](https://careplusmedicina.sharepoint.com/sites/CarePlusLabs/SitePages/arquitetura-referencia/web-api/API---RESTful.aspx) | Artigo | 0.0.1  |
| [Especificação Clean Architecture API](https://careplusmedicina.sharepoint.com/sites/CarePlusLabs/SitePages/arquitetura-referencia/Especifica%C3%A7%C3%A3o-Clean-Architecture-API.aspx) | Artigo | 0.0.1  |
| [Especificação Worker Background Services](https://careplusmedicina.sharepoint.com/sites/CarePlusLabs/SitePages/arquitetura-referencia/Especifica%C3%A7%C3%A3o-Worker-Background-Services.aspx) | Artigo | 0.0.1  |

## Trilha Desenvolvimento

A Trilha Desenvolvimento da **CarePlus** permitem que você possa **ACELERE O DESENVOLVIMENTO DA SUA CARREIRA EM TECNOLOGIA**.

| Arquitetura                                                  | Tipo  | Versão |
| :----------------------------------------------------------- | :---- | :----- |
| [Arquitetura Limpa (Clean Architecture)](https://app02.careplus.com.br/carepluslab/stream/video?view=286f0daf-1c8f-49bc-885e-cb100fd02bc0) | Vídeo | 0.0.1  |
| [Arquitetura Limpa - Commands](https://app02.careplus.com.br/carepluslab/stream/video?view=c3eb7b57-5a2c-4b24-bc6c-0a947c836d57) | Vídeo | 0.0.1  |
| [Arquitetura Limpa - Get - EFF e Dapper](https://app02.careplus.com.br/carepluslab/stream/video?view=adfc6e6a-7a9b-46ec-b13c-63eb73447fd3) | Vídeo | 0.0.1  |
| [Treinamento testes unitários](https://app02.careplus.com.br/carepluslab/stream/video?view=b221cad0-6e64-4458-97ee-508b81f8fc30) | Vídeo | 0.0.1  |