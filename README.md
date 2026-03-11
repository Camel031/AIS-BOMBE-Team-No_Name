# BOMBE POC

Check https://docs.bombe.top/ for more information

Build code by clicking `Build > Publish Selection > Publish`

graph TD
    Start[User Executes malv1.exe] --> Init[Init Dynamic APIs]
    Init --> CheckSelf{Am I 'RuntimeBroker.exe'?}
    
    %% Branch 1: Initial Execution (Migration)
    CheckSelf -- No (I am malv1.exe) --> Migrate[Copy Self to %TEMP%/RuntimeBroker.exe]
    Migrate --> SpawnReal[Spawn Real Malware<br/>(RuntimeBroker.exe)<br/><b>via PPID Spoofing</b>]
    SpawnReal --> SpawnDecoys[Spawn 5 Decoys<br/>(BOMBE_EDR_FLAG_...exe)]
    SpawnDecoys --> BecomeDecoy[<b>Become Noisy Decoy</b><br/>Execute Fake File Access]
    BecomeDecoy --> End1[Exit]

    %% Branch 2: Real Malware Execution
    CheckSelf -- Yes (I am Covert Clone) --> SilentWait[Wait 5s (Silent Mode)]
    SilentWait --> ExeChal[Execute Challenges<br/>(Registry / SQLite / Buffer)]
    ExeChal --> SendC2[Send Answer to C2<br/>(PowerShell + PPID Spoofing)]
    SendC2 --> End2[Exit & Clean Up]

    %% Styling
    style Start fill:#f9f,stroke:#333
    style SpawnReal fill:#bbf,stroke:#333,stroke-width:2px
    style BecomeDecoy fill:#faa,stroke:#333
    style SendC2 fill:#bbf,stroke:#333,stroke-width:2px