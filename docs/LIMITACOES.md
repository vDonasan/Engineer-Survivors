# Limitações conhecidas (SP1)

## Do escopo (spec §15)

- Se o anfitrião fechar a aba, a partida termina para todos (sem migração de anfitrião).
- Aba do anfitrião oculta trava a simulação: os clientes veem o aviso "O anfitrião minimizou o jogo: a partida pode travar." Pausa controlada no SP3.
- Sem reviver aliados e sem reconexão no SP1 (o personagem de quem cai da rede fica inativo).
- Arte e áudio gerados por código: qualidade abaixo de um artista dedicado.
- Valores de balanceamento iniciais; afinação das 12 células no SP4 (linha de base com bots em `docs/TESTES.md`).
- Sem suporte a celular, tablet ou controle. Navegadores-alvo: Chrome, Edge e Firefox de desktop. Safari sem garantia.
- Jogáveis no SP1: Engenharia Elétrica e Mecânica. As outras 5 engenharias já têm pixel art completa; a jogabilidade delas entra no SP2.

## Decisões de implementação com efeito visível

- **Volume por barramento em código** (geral × música/efeitos), não AudioMixer: no WebGL o AudioMixer é só parcialmente suportado. Sem efeitos de mixer.
- **Braços giratórios:** a fase desenhada é calculada pelo tempo (igual no host e no cliente); depois de uma mudança de período o braço pode aparecer alguns graus fora do ponto exato de acerto.
- **Elite:** mesmo tamanho em pixels do inimigo comum (contorno magenta e brilho), embora o raio de colisão seja 1,3×. Escala fracionária quebraria o pixel perfect.
- **Projéteis hostis no cliente** somem ~130 ms depois do fim (não há evento de remoção); inimigos somem pelo evento confiável de morte.
- **Tiros girados:** projéteis alongados (laser, seringas, resistores, celulares, garrafas) giram pelo ângulo do movimento; a rotação livre deixa a borda serrilhada em diagonais. O carro do Jean usa 8 direções desenhadas.
- **Tiros e companheiros no cooperativo** chegam no snapshot (até 200 por snapshot, os mais perto dos jogadores primeiro): com mais que isso na tela, tiros longe de todos podem piscar no cliente; o dano é sempre calculado no anfitrião.
- **Sprites dos personagens** são caricaturas em pixel art 32×32 a partir das fotos (cabelo, óculos, boné, roupa); não retratos fiéis.
- **Montagem do Cabazord Master** é só visual (1,1 s); o Master já ataca durante a animação.
- **Fonte Pixelify Sans** renderizada pelo UI Toolkit (SDF): nítida, mas não alinhada à grade de pixels como a arte do jogo.
- **Comandos de depuração na build publicada:** F4 (solo) gera 50 inimigos; `?mode=solo&bench=N` mantém N inimigos com o jogador invulnerável. Servem para medir desempenho; não funcionam no cooperativo. `&log=1&evolve=N` (solo ou anfitrião) começa com todas as armas evoluídas no nível N, para testes.
- **Primeiro áudio só após um clique** (política de reprodução automática do navegador).

## Observado nos testes

- O console mostra `ERROR: Shader` (mensagem cortada pela própria Unity) ao carregar, com e sem GPU; o jogo desenha normalmente.
- O áudio usa AAC (único formato da Unity na Web): navegadores sem esse codec, como o Chromium puro, ficam sem som.
