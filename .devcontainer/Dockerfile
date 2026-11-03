# Veilige werkomgeving voor Claude Code in auto-modus: de code staat in de container, het netwerk is beperkt tot een lijst (init-firewall.sh).
# Unity zelf draait hier niet (geen editor of licentie); de snelle tests (ci/sonar) en alle git/gh-werk wel.
FROM mcr.microsoft.com/dotnet/sdk:9.0

ARG USERNAME=claude

# Node 24 LTS (omniroute vraagt >=22.22.2; de Debian-versie is te oud).
# OmniRoute (AI-gateway/proxy): alleen geinstalleerd, niet gestart. Versie vastgezet; starten met `omniroute` (dashboard op poort 20128).
RUN apt-get update && apt-get install -y --no-install-recommends         git gh curl ca-certificates jq sudo less procps iptables ipset iproute2 dnsutils     && curl -fsSL https://deb.nodesource.com/setup_24.x | bash -     && apt-get install -y --no-install-recommends nodejs     && rm -rf /var/lib/apt/lists/*     && npm install -g @anthropic-ai/claude-code omniroute@3.8.52     && useradd -m -s /bin/bash ${USERNAME}     && echo "${USERNAME} ALL=(root) NOPASSWD: /usr/local/bin/init-firewall.sh" > /etc/sudoers.d/${USERNAME}-firewall     && chmod 0440 /etc/sudoers.d/${USERNAME}-firewall

COPY init-firewall.sh /usr/local/bin/init-firewall.sh
RUN chmod 0755 /usr/local/bin/init-firewall.sh

# Sonar-CI draait met invariante globalisatie (zie ci/sonar); lokaal hetzelfde.
ENV DOTNET_SYSTEM_GLOBALIZATION_INVARIANT=1 \
    DOTNET_CLI_TELEMETRY_OPTOUT=1 \
    DOTNET_NOLOGO=1

USER ${USERNAME}
WORKDIR /workspace
