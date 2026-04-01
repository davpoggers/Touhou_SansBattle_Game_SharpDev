using System;
using System.Collections.Generic;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Windows.Forms;
using System.Media;

namespace TouhouSansBattle_SharpDev
{
    public partial class MainForm : Form
    {
        // ══════════════════════════════════════════════════════════════════
        //  VISUAIS
        // ══════════════════════════════════════════════════════════════════
        Panel gameCanvas  = new Panel();
        PictureBox fundo    = new PictureBox();
        PictureBox bordaUI  = new PictureBox();
        PictureBox enemy    = new PictureBox();
        PictureBox player   = new PictureBox();

        // ══════════════════════════════════════════════════════════════════
        //  UI / HUD
        // ══════════════════════════════════════════════════════════════════
        Label lbl_score     = new Label();
        Label lbl_lives     = new Label();
        Label lbl_power     = new Label();
        Label lbl_boss      = new Label();
        Label lbl_gameOver  = new Label();

        //  ESTADO DO JOGO
        int hpBoss = 3500;
        int hpBossMax = 3500;
        int vidas = 3;
        int bombas = 3;
        long score = 0;
        float power = 1.0f;   // 1.0 .. 4.0
        bool gameOver = false;
        bool bombaAtiva  = false;
        int bombaTimer = 0;      // frames de invulnerabilidade
        int iframes = 0;      // iframes após morte
        bool slowMode = false;

        // boss
        int bossPhase     = 1;      // 1 ou 2
        int bossPatternTimer = 0;
        int bossX, bossY;
        int bossVX = 2, bossVY = 2;

        //  SPRITES DO JOGADOR (Reimu)
        Image[] framesIdle  = new Image[8];
        Image[] framesLeft  = new Image[8];
        Image[] framesRight = new Image[8];
        int   currentFrame  = 0;
        string currentState = "idle";

        Timer animTimer = new Timer();
        Timer gameTimer = new Timer();


        //  POSIÇÃO / HITBOX DO JOGADOR
        float playerX = 400f, playerY = 700f;
        const int PLAYER_W = 48, PLAYER_H = 64;
        const int HITBOX_R = 4;   // raio da hitbox real (pequeninho, estilo Touhou)

        // teclas pressionadas
        bool keyLeft, keyRight, keyUp, keyDown, keyShift;

        //  TIROS DO JOGADOR
        struct Bullet { public float x, y, vx, vy; public bool active; public Color color; public int w, h; }

        List<Bullet> playerBullets = new List<Bullet>();
        int playerShootCooldown = 0;

        //  DANMAKU DO BOSS
        List<Bullet> bossBullets  = new List<Bullet>();
        Random rnd = new Random();
	
	    // limite da área de jogo (relativos ao gameCanvas)
		int GAM_LEFT  = 100;
		int GAM_RIGHT = 825;
		int GAM_TOP   = 95;
		int GAM_BOT   = 920;
	        
        public MainForm()
        {
            InitializeComponent();

            this.FormBorderStyle = FormBorderStyle.None;
            this.WindowState     = FormWindowState.Maximized;
            this.BackColor       = Color.Black;
            this.KeyPreview      = true;
            this.DoubleBuffered  = true;

            // * gameCanvas 
            // tenta usar UIborder.png para obter o tamanho certo
            if (System.IO.File.Exists("Assets/scene_UI_etc/UIborder.png"))
            {
                using (Image img = Image.FromFile("Assets/scene_UI_etc/UIborder.png"))
                    gameCanvas.Size = img.Size;
            }
            else
            {
                gameCanvas.Size = new Size(1024, 768);
            }

            gameCanvas.Location = new Point(
                (Screen.PrimaryScreen.Bounds.Width  - gameCanvas.Width)  / 2,
                (Screen.PrimaryScreen.Bounds.Height - gameCanvas.Height) / 2
            );
            gameCanvas.BackColor = Color.Black;
            this.Controls.Add(gameCanvas);

            // fundo
            fundo.Bounds   = new Rectangle(0, 0, gameCanvas.Width, gameCanvas.Height);
            fundo.SizeMode = PictureBoxSizeMode.StretchImage;
            fundo.Parent   = gameCanvas;
            fundo.Load("Assets/scene_UI_etc/bg.png");

            // bordaUI
            bordaUI.Bounds    = new Rectangle(0, 0, gameCanvas.Width, gameCanvas.Height);
            bordaUI.SizeMode  = PictureBoxSizeMode.StretchImage;
            bordaUI.BackColor = Color.Transparent;
            bordaUI.Parent    = fundo;
            bordaUI.Load("Assets/scene_UI_etc/UIborder.png");

            // BOSS
            enemy.Size = new Size(180, 220);
            enemy.SizeMode = PictureBoxSizeMode.Zoom;
            enemy.BackColor = Color.Transparent;
            enemy.Parent = bordaUI;
            bossX = 190;  bossY = 80;
            enemy.Left = bossX;  enemy.Top = bossY;
       		enemy.Load("Assets/sans_enemy/enemy.png");

            // PLAYER
            player.Size = new Size(PLAYER_W, PLAYER_H);
            player.SizeMode = PictureBoxSizeMode.StretchImage;
            player.BackColor = Color.Transparent;
            player.Parent = bordaUI;
            playerX = 220; playerY = 700;
            player.Left = (int)playerX;
            player.Top = (int)playerY;

            // HUD labels
            SetupHUD();

            // Sprites 
            CarregarSprites();

            // Música
            try
            {
                SoundPlayer musica = new SoundPlayer("Assets/sfx_songs/BadApple_Megalovania.wav");
                musica.PlayLooping();
            }
            catch { }

            // ── Timers ───────────────────────────────────────────────────
            animTimer.Interval = 100;
            animTimer.Tick    += AnimTimer_Tick;
            animTimer.Start();

            gameTimer.Interval = 16;  // ~60 fps
            gameTimer.Tick    += GameTimer_Tick;
            gameTimer.Start();

            this.KeyDown += MainFormKeyDown;
            this.KeyUp   += MainFormKeyUp;

            // pintura customizada das balas
            bordaUI.Paint += BorderUI_Paint;
        }

        //  HUD

		void SetupHUD()
		{
		    int hx      = GAM_RIGHT + 25;   // ~890px — coluna da borda direita
		    int baseY   = 80;
		    Font fntBig = new Font("Consolas", 12, FontStyle.Bold);
		    Font fntMed = new Font("Consolas", 10);
		
		    lbl_boss.Parent    = bordaUI;
		    lbl_boss.Left      = hx;
		    lbl_boss.Top       = baseY;
		    lbl_boss.ForeColor = Color.OrangeRed;
		    lbl_boss.Font      = fntBig;
		    lbl_boss.AutoSize  = true;
		
		    lbl_score.Parent    = bordaUI;
		    lbl_score.Left      = hx;
		    lbl_score.Top       = baseY + 140;
		    lbl_score.ForeColor = Color.White;
		    lbl_score.Font      = fntMed;
		    lbl_score.AutoSize  = true;
		    lbl_score.Text      = "Score\n000000000";
		
		    lbl_lives.Parent    = bordaUI;
		    lbl_lives.Left      = hx;
		    lbl_lives.Top       = baseY + 250;
		    lbl_lives.ForeColor = Color.Crimson;
		    lbl_lives.Font      = fntBig;
		    lbl_lives.AutoSize  = true;
		    lbl_lives.Text      = "❤ ❤ ❤";
		
		    lbl_power.Parent    = bordaUI;
		    lbl_power.Left      = hx;
		    lbl_power.Top       = baseY + 320;
		    lbl_power.ForeColor = Color.Gold;
		    lbl_power.Font      = fntMed;
		    lbl_power.AutoSize  = true;
		    lbl_power.Text      = "Power 1.00";
		
		    lbl_gameOver.Parent     = bordaUI;
		    lbl_gameOver.Left       = GAM_LEFT + 60;
		    lbl_gameOver.Top        = (GAM_BOT - GAM_TOP) / 2 - 60;
		    lbl_gameOver.ForeColor  = Color.Red;
		    lbl_gameOver.Font       = new Font("Impact", 36, FontStyle.Bold);
		    lbl_gameOver.AutoSize   = true;
		    lbl_gameOver.Text       = "GAME OVER\nPressione ESC";
		    lbl_gameOver.Visible    = false;
		}

        void AtualizarHUD()
        {

            // Vidas
            string coracao = "";
            for (int i = 0; i < vidas; i++) coracao += "❤ ";
            lbl_lives.Text = coracao.TrimEnd();
            lbl_lives.ForeColor = Color.Crimson;

            // Bombas (estrelinhas)
            string bomba = "";
            for (int i = 0; i < bombas; i++) bomba += "✦ ";
            lbl_power.Text = "Power " + power.ToString("0.00")
                           + "\nBomba " + bomba.TrimEnd();

            // Score
            lbl_score.Text = "Score\n" + score.ToString("D9");
        }

        //  SPRITES
        void CarregarSprites()
        {
            string basePath = System.IO.Path.Combine(
                System.IO.Directory.GetCurrentDirectory(),
                "Assets", "reimu_player"
            );
            for (int i = 0; i < 8; i++)
            {
                string idle  = System.IO.Path.Combine(basePath, "reimu_idle"  + (i + 1) + ".png");
                string left  = System.IO.Path.Combine(basePath, "reimu_left"  + (i + 1) + ".png");
                string right = System.IO.Path.Combine(basePath, "reimu_right" + (i + 1) + ".png");

                framesIdle[i]  = System.IO.File.Exists(idle)  ? Image.FromFile(idle)  : null;
                framesLeft[i]  = System.IO.File.Exists(left)  ? Image.FromFile(left)  : null;
                framesRight[i] = System.IO.File.Exists(right) ? Image.FromFile(right) : null;
            }
            if (framesIdle[0] != null) player.Image = framesIdle[0];
        }

        void AnimTimer_Tick(object sender, EventArgs e)
        {
            currentFrame = (currentFrame + 1) % 8;
            Image[] frames =
                currentState == "left"  ? framesLeft  :
                currentState == "right" ? framesRight :
                                          framesIdle;
            if (frames[currentFrame] != null)
                player.Image = frames[currentFrame];
        }

        //  TECLADO
        void MainFormKeyDown(object sender, KeyEventArgs e)
        {
            if (e.KeyCode == Keys.Escape) Application.Exit();

            switch (e.KeyCode)
            {
                case Keys.Left:  case Keys.A: keyLeft  = true; break;
                case Keys.Right: case Keys.D: keyRight = true; break;
                case Keys.Up:    case Keys.W: keyUp    = true; break;
                case Keys.Down:  case Keys.S: keyDown  = true; break;
                case Keys.ShiftKey: keyShift = true; break;

                // Bomba
                case Keys.X:
                    if (!gameOver && bombas > 0 && !bombaAtiva)
                    {
                        bombas--;
                        bombaAtiva  = true;
                        bombaTimer  = 5; // ~invulnerabilidade
                        bossBullets.Clear();
                    }
                    break;
            }
        }

        void MainFormKeyUp(object sender, KeyEventArgs e)
        {
            switch (e.KeyCode)
            {
                case Keys.Left:  case Keys.A: keyLeft  = false; break;
                case Keys.Right: case Keys.D: keyRight = false; break;
                case Keys.Up:    case Keys.W: keyUp    = false; break;
                case Keys.Down:  case Keys.S: keyDown  = false; break;
                case Keys.ShiftKey: keyShift = false; break;
            }

            if (e.KeyCode != Keys.Left && e.KeyCode != Keys.A &&
                e.KeyCode != Keys.Right && e.KeyCode != Keys.D)
                return;

            if (!keyLeft && !keyRight) currentState = "idle";
        }

        //  GAME LOOP PRINCIPAL
        void GameTimer_Tick(object sender, EventArgs e)
        {
            if (gameOver) return;

            MoverJogador();
            AtirarJogador();
            MovimentarBoss();
            SpawnDanmaku();
            MoverBalas();
            CheckColisoes();
            AtualizarHUD();

            // reposiciona PictureBox do jogador
            player.Left = (int)playerX;
            player.Top  = (int)playerY;

            // bomba — flash branco
            if (bombaAtiva)
            {
                bombaTimer--;
                bordaUI.BackColor = (bombaTimer % 6 < 3)
                    ? Color.FromArgb(80, 255, 255, 255)
                    : Color.Transparent;
                if (bombaTimer <= 0)
                {
                    bombaAtiva = false;
                    bordaUI.BackColor = Color.Transparent;
                }
            }

            // iframes após morte (pisca o player)
            if (iframes > 0)
            {
                iframes--;
                player.Visible = (iframes % 6 < 4);
            }
            else
            {
                player.Visible = true;
            }

            bordaUI.Invalidate(); // forçar repintura das balas
        }


        //  MOVIMENTO DO JOGADOR
        void MoverJogador()
        {
            slowMode = keyShift;
            float spd = slowMode ? 2.5f : 5.0f;

            if (keyLeft)  { playerX -= spd; currentState = "left";  }
            if (keyRight) { playerX += spd; currentState = "right"; }
            if (!keyLeft && !keyRight) currentState = "idle";
            if (keyUp)    playerY -= spd;
            if (keyDown)  playerY += spd;

            // clamp dentro da área de jogo
            if (playerX < GAM_LEFT)                      playerX = GAM_LEFT;
            if (playerX + PLAYER_W > GAM_RIGHT)          playerX = GAM_RIGHT - PLAYER_W;
            if (playerY < GAM_TOP)                       playerY = GAM_TOP;
            if (playerY + PLAYER_H > GAM_BOT)            playerY = GAM_BOT  - PLAYER_H;
        }

        //  TIRO DO JOGADOR
        void AtirarJogador()
        {
            if (playerShootCooldown > 0) { playerShootCooldown--; return; }

            // Reimu atira em modo automático (Z = disparo, mas aqui é contínuo)
            // Power determina o número de colunas
            int cols = (int)Math.Ceiling(power);  // 1..4 colunas

            float cx = playerX + PLAYER_W / 2;
            float cy = playerY;

            // colunas simétricas
            float[] offsets = { 0, -12, 12, -24, 24 };
            for (int i = 0; i < cols; i++)
            {
                float ox = offsets[Math.Min(i, offsets.Length - 1)];
                SpawnPlayerBullet(cx + ox, cy, 0, -18, Color.Aqua, 6, 16);

                // colunas laterais => leve âncora diagonal
                if (i == 1 || i == 3)
                    SpawnPlayerBullet(cx + ox, cy, -1.5f, -17, Color.LightCyan, 4, 12);
                if (i == 2 || i == 4)
                    SpawnPlayerBullet(cx + ox, cy,  1.5f, -17, Color.LightCyan, 4, 12);
            }

            playerShootCooldown = 5;
        }

        void SpawnPlayerBullet(float x, float y, float vx, float vy, Color c, int w, int h)
        {
            playerBullets.Add(new Bullet {
                x = x, y = y, vx = vx, vy = vy,
                active = true, color = c, w = w, h = h
            });
        }

        //  BOSS
        void MovimentarBoss()
        {
            // fase 2 quando boss ficar abaixo de 50%
            if (hpBoss <= hpBossMax / 2 && bossPhase == 1)
            {
                bossPhase      = 2;
                bossVX         = 3;
                bossVY         = 3;
                enemy.Size     = new Size(140, 170);
            }

            bossX += bossVX;
            bossY += bossVY;

            // bounce nas bordas da área de jogo
            if (bossX < GAM_LEFT || bossX + enemy.Width > GAM_RIGHT)
                bossVX = -bossVX;
            if (bossY < GAM_TOP || bossY + enemy.Height > GAM_TOP + 200)
                bossVY = -bossVY;

            enemy.Left = bossX;
            enemy.Top  = bossY;
        }


        //  DANMAKU (padrões de balas do boss)
        void SpawnDanmaku()
        {
            bossPatternTimer++;

            float bcx = bossX + enemy.Width  / 2f;
            float bcy = bossY + enemy.Height / 2f;

            if (bossPhase == 1)
            {
                // Fase 1 spray circular puro a cada 60 frames
                if (bossPatternTimer % 60 == 0)
                    SpawnCircle(bcx, bcy, 16, 2.5f, Color.OrangeRed, 10, 10);

                // Aimed shot a cada 90 frames
                if (bossPatternTimer % 90 == 0)
                    SpawnAimed(bcx, bcy, 3, 3.5f, Color.MediumPurple, 8, 14);
            }
            else
            {
                // Fase 2 espiral densa + aimed mais rápido
                if (bossPatternTimer % 10 == 0)
                {
                    float angle = (bossPatternTimer * 7f) % 360;
                    SpawnSpiral(bcx, bcy, 12, 3.5f, angle, Color.HotPink, 8, 8);
                }
                if (bossPatternTimer % 50 == 0)
                    SpawnAimed(bcx, bcy, 5, 4.5f, Color.Yellow, 7, 12);
            }
        }

        void SpawnCircle(float ox, float oy, int n, float speed, Color c, int w, int h)
        {
            for (int i = 0; i < n; i++)
            {
                float a = (float)(2 * Math.PI * i / n);
                bossBullets.Add(new Bullet {
                    x = ox, y = oy,
                    vx = (float)(speed * Math.Cos(a)),
                    vy = (float)(speed * Math.Sin(a)),
                    active = true, color = c, w = w, h = h
                });
            }
        }

        void SpawnSpiral(float ox, float oy, int n, float speed, float baseAngleDeg, Color c, int w, int h)
        {
            for (int i = 0; i < n; i++)
            {
                float a = (float)((baseAngleDeg + 360.0 * i / n) * Math.PI / 180);
                bossBullets.Add(new Bullet {
                    x = ox, y = oy,
                    vx = (float)(speed * Math.Cos(a)),
                    vy = (float)(speed * Math.Sin(a)),
                    active = true, color = c, w = w, h = h
                });
            }
        }

        void SpawnAimed(float ox, float oy, int n, float speed, Color c, int w, int h)
        {
            // aponta para o jogador com leve spread
            float dx = playerX + PLAYER_W / 2 - ox;
            float dy = playerY + PLAYER_H / 2 - oy;
            float baseAng = (float)Math.Atan2(dy, dx);

            float spread = (float)(Math.PI / 8);
            for (int i = 0; i < n; i++)
            {
                float a = baseAng + spread * (i - n / 2);
                bossBullets.Add(new Bullet {
                    x = ox, y = oy,
                    vx = (float)(speed * Math.Cos(a)),
                    vy = (float)(speed * Math.Sin(a)),
                    active = true, color = c, w = w, h = h
                });
            }
        }

        //  MOVER TODAS AS BALAS
        void MoverBalas()
        {
            int margin = 60;
            for (int i = playerBullets.Count - 1; i >= 0; i--)
            {
                var b = playerBullets[i];
                b.x += b.vx;  b.y += b.vy;
                if (b.x < -margin || b.x > GAM_RIGHT + margin ||
                    b.y < -margin || b.y > GAM_BOT   + margin)
                    b.active = false;
                playerBullets[i] = b;
            }
            playerBullets.RemoveAll(b => !b.active);

            for (int i = bossBullets.Count - 1; i >= 0; i--)
            {
                var b = bossBullets[i];
                b.x += b.vx;  b.y += b.vy;
                if (b.x < -margin || b.x > GAM_RIGHT + margin ||
                    b.y < -margin || b.y > GAM_BOT   + margin)
                    b.active = false;
                bossBullets[i] = b;
            }
            bossBullets.RemoveAll(b => !b.active);
        }


        //  COLISÕES
        void CheckColisoes()
        {
            // Hitbox do boss
            Rectangle bossRect = new Rectangle(
                bossX + 20, bossY + 20,
                enemy.Width - 40, enemy.Height - 40
            );

            // Tiros do jogador x boss
            for (int i = playerBullets.Count - 1; i >= 0; i--)
            {
                var b = playerBullets[i];
                if (!b.active) continue;
                Rectangle br = new Rectangle((int)b.x - b.w/2, (int)b.y - b.h/2, b.w, b.h);
                if (br.IntersectsWith(bossRect))
                {
                    b.active = false;
                    playerBullets[i] = b;

                    hpBoss -= rnd.Next(8, 16);
                    score  += 100;

                    if (power < 4.0f) power = Math.Min(4.0f, power + 0.02f);

                    if (hpBoss <= 0)
                    {
                        hpBoss = 0;
                        enemy.Visible  = false;
                        gameOver       = true;
                        lbl_gameOver.Text    = "STAGE CLEAR!\nPressione ESC";
                        lbl_gameOver.ForeColor = Color.LimeGreen;
                        lbl_gameOver.Visible = true;
                    }
                }
            }

            // Balas do boss x hitbox do jogador
            if (iframes > 0 || bombaAtiva) return;  // invulnerável

            float hcx = playerX + PLAYER_W / 2f;
            float hcy = playerY + PLAYER_H / 2f - 4f;

            for (int i = bossBullets.Count - 1; i >= 0; i--)
            {
                var b = bossBullets[i];
                if (!b.active) continue;

                float dist = (float)Math.Sqrt(
                    (b.x - hcx) * (b.x - hcx) +
                    (b.y - hcy) * (b.y - hcy)
                );

                if (dist < HITBOX_R + b.w / 2)
                {
                    bossBullets.RemoveAt(i);
                    PlayerMorre();
                    break;
                }
            }
        }

        void PlayerMorre()
        {
            vidas--;
            iframes = 120;   // ~2 seg de iframes
            bossBullets.Clear();
            power = Math.Max(1.0f, power - 1.0f);

            if (vidas <= 0)
            {
                gameOver = true;
                lbl_gameOver.Text    = "GAME OVER\nPressione ESC";
                lbl_gameOver.ForeColor = Color.Red;
                lbl_gameOver.Visible = true;
            }
        }

        //  PINTURA USTOMIZADA (balas, hitbox)
        void BorderUI_Paint(object sender, PaintEventArgs e)
        {
            Graphics g = e.Graphics;
            g.SmoothingMode = SmoothingMode.AntiAlias;

            // Balas do jogador — elipses azuis/ciano brilhantes
            foreach (var b in playerBullets)
            {
                if (!b.active) continue;
                DrawGlowBullet(g, b.x, b.y, b.w, b.h, b.color, Color.White);
            }

            // Balas do boss — círculos coloridos
            foreach (var b in bossBullets)
            {
                if (!b.active) continue;
                Color inner = Color.White;
                DrawGlowBullet(g, b.x, b.y, b.w, b.h, b.color, inner);
            }

            // Hitbox do jogador (só no modo slow)
            if (slowMode && !gameOver)
            {
                float hcx = playerX + PLAYER_W / 2f;
                float hcy = playerY + PLAYER_H / 2f - 4f;
                int r = HITBOX_R + 6;
                g.DrawEllipse(new Pen(Color.Red, 2), hcx - r, hcy - r, r * 2, r * 2);
                g.FillEllipse(new SolidBrush(Color.FromArgb(180, 255, 0, 0)),
                              hcx - HITBOX_R, hcy - HITBOX_R, HITBOX_R * 2, HITBOX_R * 2);
            }

            // Boss HP bar gráfica
            DrawBossBar(g);
        }

        void DrawGlowBullet(Graphics g, float x, float y, int w, int h, Color outer, Color inner)
        {
            // halo externo
            using (var brush = new SolidBrush(Color.FromArgb(80, outer)))
                g.FillEllipse(brush, x - w - 2, y - h - 2, (w + 2) * 2, (h + 2) * 2);
            // corpo
            using (var brush = new SolidBrush(outer))
                g.FillEllipse(brush, x - w / 2, y - h / 2, w, h);
            // brilho central
            using (var brush = new SolidBrush(Color.FromArgb(200, inner)))
                g.FillEllipse(brush, x - w / 4, y - h / 4, w / 2, h / 2);
        }

        void DrawBossBar(Graphics g)
        {
            if (hpBoss <= 0) return;
            int barW = GAM_RIGHT - GAM_LEFT;
            int barH = 8;
            int bx = GAM_LEFT;
            int by = GAM_TOP - 12;

            // fundo escuro
            g.FillRectangle(Brushes.DarkRed, bx, by, barW, barH);

            // barra de HP (gradiente vermelho → amarelo)
            float pct = (float)hpBoss / hpBossMax;
            using (var br = new LinearGradientBrush(
                new RectangleF(bx, by, barW * pct, barH),
                Color.OrangeRed, Color.Yellow, LinearGradientMode.Horizontal))
            {
                g.FillRectangle(br, bx, by, barW * pct, barH);
            }

            // borda
            g.DrawRectangle(new Pen(Color.White, 1), bx, by, barW, barH);

            // texto de info do boss
            string bossName = bossPhase == 1
                ? "☆ Sans (Fase 1)"
                : "☆☆ Sans (Fase 2 - Last Spell)";
            g.DrawString(bossName,
                new Font("Consolas", 9, FontStyle.Bold),
                Brushes.White, bx, by - 14);
        }
    }
}