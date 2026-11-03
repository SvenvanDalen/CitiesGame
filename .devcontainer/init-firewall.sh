#!/bin/bash
# Beperkt uitgaand verkeer tot een lijst: GitHub, Anthropic, npm, NuGet en SonarCloud. Al het andere wordt geweigerd.
# Draait als root bij elke start van de container (postStartCommand, via sudo).
set -euo pipefail

DOMAINS=(
    api.anthropic.com statsig.anthropic.com sentry.io
    registry.npmjs.org
    api.nuget.org www.nuget.org globalcdn.nuget.org
    sonarcloud.io api.sonarcloud.io scanner.sonarcloud.io sonarsource.com binaries.sonarsource.com
    github.com api.github.com objects.githubusercontent.com raw.githubusercontent.com
)

iptables -F && iptables -X
ipset destroy allowed 2>/dev/null || true
ipset create allowed hash:net

# Loopback en DNS (nodig om de lijst op te lossen) altijd toestaan.
iptables -A INPUT -i lo -j ACCEPT
iptables -A OUTPUT -o lo -j ACCEPT
iptables -A OUTPUT -p udp --dport 53 -j ACCEPT
iptables -A INPUT -p udp --sport 53 -j ACCEPT

# GitHub publiceert zijn bereiken; de rest wordt per domein opgelost.
for cidr in $(curl -fsS https://api.github.com/meta | jq -r '(.web + .api + .git)[]' | grep -v ':'); do
    ipset add allowed "$cidr" -exist
done
for domain in "${DOMAINS[@]}"; do
    for ip in $(dig +short A "$domain" | grep -E '^[0-9.]+$'); do
        ipset add allowed "$ip" -exist
    done
done

# Het hostnetwerk (voor VS Code) en bestaande verbindingen blijven werken.
host_net=$(ip route | awk '/default/ {print $3}' | sed 's/\.[0-9]*$/.0\/24/')
iptables -A INPUT -s "$host_net" -j ACCEPT
iptables -A OUTPUT -d "$host_net" -j ACCEPT
iptables -A INPUT -m state --state ESTABLISHED,RELATED -j ACCEPT
iptables -A OUTPUT -m state --state ESTABLISHED,RELATED -j ACCEPT
iptables -A OUTPUT -m set --match-set allowed dst -j ACCEPT

iptables -P INPUT DROP
iptables -P FORWARD DROP
iptables -P OUTPUT DROP

# Controle: een willekeurige site moet nu geweigerd worden en GitHub niet.
if curl -fsS --max-time 5 https://example.com >/dev/null 2>&1; then
    echo "Firewall werkt niet: example.com is bereikbaar" >&2
    exit 1
fi
curl -fsS --max-time 10 https://api.github.com/zen >/dev/null || { echo "Firewall blokkeert GitHub" >&2; exit 1; }
echo "Firewall actief."
