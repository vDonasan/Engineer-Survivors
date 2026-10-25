# Personagens jogáveis — design (SP2a)

Pedido do autor: dez personagens com ataque exclusivo, evolução que acrescenta mecânica (sem tirar o ataque
inicial, exceto o Cabazord Master), sprites 32×32 baseados nas fotos de `fotos sprites/`, efeitos, sons, solo e
cooperativo de 1 a 4 jogadores. O autor pediu para eu decidir e executar sozinho; as decisões estão aqui.

## Modelo de dados

- Cada personagem é uma entrada de `specializations` (o código já trata a escolha do jogador por esse id) com
  campos novos `engineering` (texto exibido) e `hidden` (as especializações Elétrica/Mecânica do SP1 ficam
  ocultas da seleção, mas continuam válidas para testes e bots).
- Arma exclusiva por personagem (`exclusive: true`): nunca aparece como carta para outro personagem.
- Níveis 1–5 da arma exclusiva mudam comportamento (nível 3 e 5 destravam mecânicas, não só números).
- Evolução: arma exclusiva no nível 5 + um componente temático no nível 2. A arma evoluída mantém o ataque
  original e acrescenta o novo; tem 3 níveis e o nível 3 é a melhoria **avançada**.
- Especial: Giulia usa o Apagão, Badotti a Turbina de Emergência, os demais (Computação) o novo **Overclock**
  (recarga −40% e +15% de velocidade por 6 s).
- Componentes novos (um por evolução): Cartão Corporativo, Café Expresso, Halter, Marmita, Pote de Whey,
  Kit Arduino, Cabo HDMI, Megafone; Bateria Portátil e Turbina continuam.
- Ajuste por dificuldade × equipe: novo parâmetro `abilityDamage` em dificuldade/equipe/ajuste de célula.

| Personagem | Arma (nv. 3 / nv. 5) | Evolução (+ componente) | Avançada (evo nv. 3) |
|---|---|---|---|
| Staut | Bolhas que flutuam em zigue-zague e estouram em área (bolha-prisão / bolhas grandes perfuram) | Maletas de dinheiro perfurantes que abrem em moedas (Cartão Corporativo) | moedas ricocheteiam |
| Miyuki | Rajadas de dígitos; expressão perfurante; equação explosiva com aviso (dígitos ricocheteiam / ricochete 2) | Sushi em curva e sashimi ricocheteante; dígitos marcam, sushi em alvo marcado explode (Café Expresso) | esteira de pratos em cadeia |
| Giulia | Resistores perfurantes e transistores com descarga em cadeia; acertos agrupados criam campo elétrico | Fontes (pulso em área), osciloscópio (onda em linha), circuito entre inimigos (Bateria Portátil) | tempestade eletromagnética |
| Breno | Hambúrguer com empurrão e onda ao cair; rajada de Doritos perfurantes (Doritos ricocheteiam / mais perfuração) | Pneus que rolam, quicam nas paredes e geram onda ao parar (Halter) | pneus orbitais |
| Artur | Socos em cone, combo termina em golpe carregado com onda de choque (interrupção / combo a cada 3) | Pílulas em rajada e seringas perfurantes; seringa especial explode (Marmita) | soco carregado dispara anel de seringas |
| Badotti | Três Cabazords autônomos: atirador, brigão e investida; ataque combinado no mesmo alvo | Cabazord Master substitui os três: soco em área, disparo, investida, golpe no chão (Kit Arduino) | salto combinado com Badotti |
| Garcia | Celulares que ricocheteiam; leque de papéis de cola que grudam e explodem depois | Computadores (impacto + fragmentos), TVs caindo em área marcada (Cabo HDMI) | chuva de eletrônicos |
| Jean | Montado no porco; jabutis em curva que continuam andando no chão (ricochete / perseguição) | Carro em drift circulando Jean, rastro de pneus, derrapagem ampla (Turbina) | volta acelerada com zona de impacto |
| Fufu | Socos em arco largo com empurrão; sequência acumula fúria (velocidade de ataque) e golpe carregado | Seringas de insulina perfurantes e frascos que estouram em área (Pote de Whey) | fúria máxima: socos disparam seringas |
| Donasa | Anel sonoro com empurrão/interrupção, onda direcional perfurante, grito periódico | Ondas maiores e garrafas verdes que quebram em área (Megafone) | grito gigante + chuva de garrafas |

## Simulação (host autoritativo)

Sistemas reutilizáveis em `Sim/Abilities/Kit`:
- **Tiros** (`ShotStore`): movimento linear, ondulado, curvo, teleguiado ou rolando com quique; perfuração,
  ricochete, explosão no acerto/fim, lentidão, atordoamento, marca, cola e prisão. Fim de tiro chama o
  comportamento dono (`IShotHandler`) para filhos (moedas, fragmentos) com profundidade máxima 2.
- **Zonas** (campo elétrico, tempestade), **golpes atrasados** (equação, TV: aviso + explosão),
  **companheiros** (Cabazords, carro, pneus orbitais), **golpe em cone** e **anel de choque**, **cadeia**.
- Estados de inimigo: marca, cola (dano atrasado), dano contínuo; prisão = atordoamento + dano contínuo.
- Limites: 384 tiros, 32 zonas, 64 golpes atrasados, 24 companheiros; cadeias com saltos limitados e
  alvos sem repetição; filhos só até profundidade 2.

## Rede

- Snapshot versão 2: nova sequência **aliados** (id, x, y, sprite, parâmetro = 8 B) com tiros, zonas e
  companheiros, até 200 por snapshot, perto dos jogadores primeiro. Os clientes desenham por interpolação,
  como os inimigos; tiro consumido some no cliente.
- Inimigos ganham 1 byte opcional de estado visual (preso, marcado, colado).
- Eventos cosméticos novos: explosão em área, aviso de área, golpe em cone, disparo (som). Números de dano
  só ocupam metade da cota de eventos, para não apagar efeitos de ataque.

## Apresentação

- Sprites dos 10 personagens (todas as animações existentes, 4 direções) e retratos 64×64 com as 9 emoções,
  baseados nas fotos: boné, óculos, cabelo, bigode, roupa. Jean desenhado montado no porco.
- Sprites de projéteis, companheiros (Cabazords 24×24, Master 48×48, carro 32×24) e efeitos.
- Sons sintetizados por ataque (AudioSource), escolhidos pelo campo `sfx` da habilidade.
- Montagem do Cabazord Master com animação própria; rastro de pneus e fumaça do carro.

## Testes

- Sim (dotnet): cada mecânica (prisão, ricochete, marca, cola, campo, cadeia, companheiros, evolução sem
  perder o ataque inicial, Master substitui os robôs, limites de cadeia), codec v2 e partidas completas
  por personagem com bots, solo e 4 jogadores, medindo tempo por tick.
- Unity: EditMode/PlayMode existentes + sprites de todos os personagens presentes.
- Navegador: prova de rede com dois personagens novos evoluídos e captura de tela.
