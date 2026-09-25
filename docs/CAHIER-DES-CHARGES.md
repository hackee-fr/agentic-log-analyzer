# Agentic Log Analyzer

> Plateforme agentique d’analyse, de corrélation et d’investigation de logs.

**Version :** 0.1 — V1  
**Statut :** Draft  
**Technologie principale :** C# / .NET  
**LLM local initial :** Ollama

## 1. Vision

Agentic Log Analyzer est une plateforme extensible permettant de collecter, parser, normaliser, rechercher, corréler et analyser des événements provenant de différentes sources de logs.

La couche agentique doit venir au-dessus d’un moteur de logs fiable. Le LLM n’est jamais la source de vérité : les faits doivent provenir des événements et des outils exécutables.

## 2. Sources ciblées

La plateforme doit rester agnostique vis-à-vis des sources.

Sources initiales ou futures :

- Micro-Sésame
- Wazuh
- Windows Event Log
- Linux / Syslog
- pare-feux
- équipements réseau
- applications
- Docker / Kubernetes
- autres sources via connecteurs

Le format Micro-Sésame définitif ne doit pas être inventé : il sera basé sur des exemples réels anonymisés.

## 3. Architecture

```text
Sources
  ↓
Connectors
  ↓
Parsing
  ↓
Normalization
  ↓
Storage
  ↓
Detection / Search
  ↓
Correlation
  ↓
Tools
  ↓
Skills
  ↓
Agents
  ↓
LLM
  ↓
Investigation
  ↓
Report
```

## 4. Principes

- Source agnostique.
- Séparation stricte des responsabilités.
- Fonctionnement du moteur de logs sans LLM.
- LLM interchangeable via une abstraction.
- Traçabilité des actions agentiques.
- Séparation entre faits, hypothèses et recommandations.
- Aucune donnée professionnelle sensible dans Git.

## 5. Modèle canonique

Exemple conceptuel :

```json
{
  "id": "uuid",
  "timestamp": "2026-09-24T08:42:01Z",
  "source": {
    "type": "microsesame",
    "name": "access-control-01"
  },
  "event": {
    "category": "authentication",
    "action": "access",
    "result": "failure"
  },
  "actor": {
    "user": "john.doe",
    "identifier": "12345"
  },
  "target": {
    "device": "reader-01",
    "location": "building-a"
  },
  "network": {
    "source_ip": "192.168.1.50"
  },
  "raw": {}
}
```

## 6. Connecteurs

Interface cible :

```csharp
public interface ILogConnector
{
    string Name { get; }

    Task<IEnumerable<RawLog>> ReadAsync(
        CancellationToken cancellationToken);
}
```

Connecteurs envisagés :

- FileConnector
- WindowsEventLogConnector
- LinuxSyslogConnector
- WazuhConnector
- MicroSesameConnector
- HttpConnector

## 7. Détection et corrélation

Le moteur doit fonctionner sans LLM.

Exemples :

- plusieurs authentifications échouées dans une fenêtre temporelle ;
- succès après plusieurs échecs ;
- activité inhabituelle d’un compte ;
- événements liés au même utilisateur, IP, machine ou session ;
- corrélation de plusieurs sources.

## 8. Architecture agentique

Composants :

- Agents : comportement et orchestration.
- Skills : capacités spécialisées.
- Tools : opérations exécutables.
- Knowledge : connaissances statiques.
- Memory : contexte d’investigation.
- Orchestrator : coordination.
- LLM Provider : abstraction du moteur de langage.

Agents envisagés :

- DetectionAgent
- InvestigationAgent
- CorrelationAgent
- ReportingAgent

Skills envisagés :

- search-events
- detect-anomaly
- correlate-events
- build-timeline
- investigate-user
- investigate-device
- generate-report

Tools envisagés :

- search_logs
- get_event
- get_timeline
- find_related_events
- get_user_activity
- get_device_activity
- run_detection
- get_source_status

## 9. LLM

Abstraction :

```csharp
public interface ILlmProvider
{
    Task<string> GenerateAsync(
        string systemPrompt,
        string prompt,
        CancellationToken cancellationToken);
}
```

Première implémentation : `OllamaLlmProvider`.

Implémentations futures possibles : OpenAI, Anthropic, Mistral et autres fournisseurs compatibles.

## 10. Stockage

PostgreSQL est le stockage initial envisagé.

Entités principales :

- events
- sources
- detections
- correlations
- investigations
- reports
- agent_runs
- tool_calls
- audit_logs

## 11. API

Endpoints initiaux envisagés :

```text
GET  /api/events
GET  /api/events/{id}
GET  /api/sources
GET  /api/detections
GET  /api/correlations
GET  /api/investigations
POST /api/investigations
GET  /api/timelines
POST /api/agents/investigate
GET  /api/reports
```

## 12. Interface

Dashboard futur : Overview, Events, Sources, Detections, Correlations, Investigations, Agents, Reports.

Une investigation doit afficher : question, contexte, timeline, événements, corrélations, actions de l’agent, appels d’outils, findings et rapport.

## 13. Sécurité

Le système doit prévoir :

- authentification ;
- autorisation ;
- gestion sécurisée des secrets ;
- validation des entrées ;
- chiffrement des communications ;
- audit des actions ;
- séparation des privilèges.

Les secrets ne doivent jamais être commités.

## 14. Tests

Niveaux prévus :

- unit tests ;
- integration tests ;
- agent tests ;
- end-to-end tests.

Les fixtures doivent être synthétiques ou anonymisées.

## 15. Hyper-V Lab

### VM principale

- Nom : AI-ANALYZER
- Ubuntu Server 24.04 LTS
- 8 vCPU
- 16–24 GB RAM
- 100+ GB disque
- IP cible : 10.10.20.10

### VM source

- Nom : LOG-SOURCE
- Ubuntu Server 24.04 LTS
- 2–4 vCPU
- 4–8 GB RAM
- IP cible : 10.10.20.20

Réseau : `LAB-AI` — `10.10.20.0/24`.

Kubernetes, Kafka et autres architectures distribuées sont hors périmètre initial.

## 16. CI/CD

GitHub Actions devra progressivement automatiser :

```text
Push
 ↓
Restore
 ↓
Build
 ↓
Tests
 ↓
Security checks
 ↓
Docker build
 ↓
Deploy
```

## 17. Structure cible

```text
agentic-log-analyzer/
├── .github/workflows/
├── agents/
├── skills/
├── knowledge/
├── docs/
│   └── CAHIER-DES-CHARGES.md
├── src/
├── tests/
├── AGENTS.md
├── .gitignore
├── README.md
└── LICENSE
```

## 18. Roadmap

1. Cahier des charges
2. Hyper-V lab
3. Solution C#/.NET
4. Domain
5. Application
6. Infrastructure
7. FileConnector
8. Parsing
9. Canonical Event
10. PostgreSQL
11. Search
12. Timeline
13. Detection
14. Correlation
15. Tools
16. Skills
17. Agents
18. Ollama
19. Agentic runtime
20. API
21. Dashboard
22. Micro-Sésame
23. Docker
24. CI/CD

## 19. Critères V1

La V1 doit pouvoir :

1. recevoir des logs ;
2. les parser ;
3. les normaliser ;
4. les stocker ;
5. les rechercher ;
6. construire une timeline ;
7. détecter un comportement défini ;
8. corréler plusieurs événements ;
9. exposer ces fonctions via une API ;
10. permettre à un agent d’utiliser des outils ;
11. utiliser Ollama ;
12. tracer les actions de l’agent ;
13. générer un rapport ;
14. fonctionner dans le laboratoire Hyper-V.

## 20. Règle de développement

Le développement commence par le moteur de logs :

```text
log
 ↓
parse
 ↓
normalize
 ↓
store
 ↓
search
 ↓
correlate
```

Puis seulement la couche agentique :

```text
agent
 ↓
LLM
 ↓
tools
```

Cette séparation doit rester une contrainte architecturale du projet.
