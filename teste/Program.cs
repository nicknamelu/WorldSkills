using System.Drawing.Imaging;
public class MiniPaint : Form
{
    private DateTime lastClickTime = DateTime.MinValue;
    private Panel drawingPanel;
    private Panel topPanel;
    private Bitmap[] layers;
    private Bitmap canvas;
    private Random rnd = new Random();
    private const int DoubleClickInterval = 300; // ms
    private int brushSize = 3;
    private int ellipseWidth = 150;
    private int ellipseHeight = 100;
    private int sprayDensity = 10; // valor inicial
    private int brushPaintMax = 100; // quantidade máxima de tinta
    private int brushPaint; // quantidade atual de tinta
    private int activeLayer = 0;
    private int layerCount = 5;
    private enum ShapeType { None, Ellipse, Circle, Square, Triangle }
    private enum ToolType { Pen, Eraser, Spray, Brush }
    private bool brushDepleted = false;
    private bool isDrawing = false;
    private bool previewActive = false;
    private bool isDoubleClickDrawing = false;
    private Point previewLocation = Point.Empty;
    private Point lastPoint;
    private Color currentColor = Color.Black;
    private Color[] customColors = new Color[16];
    private ToolType currentTool = ToolType.Pen;
    private ShapeType currentShape = ShapeType.None;
    private Graphics graphics;

    public MiniPaint()
    {
        this.Text = "Mini Paint++";
        this.Size = new Size(900, 600);

        topPanel = new Panel { Dock = DockStyle.Top, Height = 50, BackColor = Color.LightGray };
        this.Controls.Add(topPanel);

        drawingPanel = new DoubleBufferedPanel { Dock = DockStyle.Fill, BackColor = Color.White };
        this.Controls.Add(drawingPanel);

        layers = new Bitmap[layerCount];
        for (int i = 0; i < layerCount; i++)
            layers[i] = new Bitmap(this.ClientSize.Width, this.ClientSize.Height - topPanel.Height);
        canvas = layers[0];
        graphics = Graphics.FromImage(canvas);

        drawingPanel.Paint += DrawingPanel_Paint;
        drawingPanel.MouseDown += DrawingPanel_MouseDown;
        drawingPanel.MouseMove += Draw;
        drawingPanel.MouseMove += DrawingPanel_MouseMove;
        drawingPanel.MouseUp += StopDrawing;
        drawingPanel.MouseClick += DrawingPanel_MouseClick;

        Button btnColor = new Button { Text = "Cor", Location = new Point(10, 10) };
        Button btnClear = new Button { Text = "Limpar", Location = new Point(80, 10) };
        Button btnSave = new Button { Text = "Salvar", Location = new Point(160, 10) };
        Button btnSize = new Button { Text = "Tamanho", Location = new Point(240, 10) };
        Button btnForms = new Button { Text = "Formas", Location = new Point(320, 10) };
        Button btnLayers = new Button { Text = "Camadas", Location = new Point(400, 10) };
        Button btnTools = new Button { Text = "Ferramentas", Location = new Point(480, 10) };
        btnColor.Click += ChooseColor;
        btnClear.Click += ClearCanvas;
        btnSave.Click += SaveImage;
        btnSize.Click += ChangeSizeBrush;
        btnForms.Click += (s, e) => ChooseShape();
        btnLayers.Click += (s, e) => ShowLayerSelector();
        btnTools.Click += (s, e) => ShowToolSelector();
        topPanel.Controls.Add(btnColor);
        topPanel.Controls.Add(btnClear);
        topPanel.Controls.Add(btnSave);
        topPanel.Controls.Add(btnSize);
        topPanel.Controls.Add(btnForms);
        topPanel.Controls.Add(btnLayers);
        topPanel.Controls.Add(btnTools);

        // Inicializa tinta do pincel e cores personalizadas
        brushPaint = brushPaintMax;
        for (int i = 0; i < customColors.Length; i++)
            customColors[i] = Color.White;
    }

    private void DrawingPanel_Paint(object sender, PaintEventArgs e)
    {
        for (int i = 0; i < layerCount; i++)
            e.Graphics.DrawImage(layers[i], Point.Empty);

        if (previewActive && currentShape != ShapeType.None && previewLocation != Point.Empty)
        {
            using (Pen pen = new Pen(currentColor, brushSize) { DashStyle = System.Drawing.Drawing2D.DashStyle.Dash })
            {
                switch (currentShape)
                {
                    case ShapeType.Ellipse:
                        DrawEllipsePreview(e.Graphics, previewLocation);
                        break;
                    case ShapeType.Circle:
                        DrawCirclePreview(e.Graphics, previewLocation);
                        break;
                    case ShapeType.Square:
                        DrawSquarePreview(e.Graphics, previewLocation);
                        break;
                    case ShapeType.Triangle:
                        DrawTrianglePreview(e.Graphics, previewLocation);
                        break;
                }
            }
        }
    }

    private void DrawingPanel_MouseDown(object sender, MouseEventArgs e)
    {
        if (!previewActive)
        {
            // Detecta duplo clique
            var now = DateTime.Now;
            if ((now - lastClickTime).TotalMilliseconds < DoubleClickInterval)
            {
                isDoubleClickDrawing = !isDoubleClickDrawing;
            }
            lastClickTime = now;

            isDrawing = true;
            lastPoint = e.Location;
        }
    }

    private void Draw(object sender, MouseEventArgs e)
    {
        if (previewActive) return;
        if (isDrawing)
        {
            // desenhar na camada ativa
            canvas = layers[activeLayer];
            graphics = Graphics.FromImage(canvas);
            graphics.SmoothingMode = System.Drawing.Drawing2D.SmoothingMode.AntiAlias;
            switch (currentTool)
            {
                case ToolType.Pen:
                    using (Pen pen = new Pen(currentColor, brushSize))
                    {
                        pen.StartCap = System.Drawing.Drawing2D.LineCap.Round;
                        pen.EndCap = System.Drawing.Drawing2D.LineCap.Round;
                        graphics.DrawLine(pen, lastPoint, e.Location);
                    }
                    break;
                case ToolType.Eraser:
                    using (Pen eraser = new Pen(Color.White, brushSize * 2))
                    {
                        eraser.StartCap = System.Drawing.Drawing2D.LineCap.Round;
                        eraser.EndCap = System.Drawing.Drawing2D.LineCap.Round;
                        graphics.DrawLine(eraser, lastPoint, e.Location);
                    }
                    break;
                case ToolType.Spray:
                    for (int i = 0; i < sprayDensity; i++)
                    {
                        int dx = rnd.Next(-brushSize, brushSize + 1);
                        int dy = rnd.Next(-brushSize, brushSize + 1);
                        if (dx * dx + dy * dy <= brushSize * brushSize)
                            graphics.FillEllipse(new SolidBrush(currentColor), e.Location.X + dx, e.Location.Y + dy, 1, 1);
                    }
                    break;
                case ToolType.Brush:
                    if (!brushDepleted && brushPaint > 0)
                    {
                        using (SolidBrush brush = new SolidBrush(currentColor))
                        {
                            graphics.FillEllipse(brush, e.Location.X - brushSize, e.Location.Y - brushSize, brushSize * 2, brushSize * 2);
                        }
                        brushPaint--;
                        if (brushPaint <= 0)
                        {
                            brushDepleted = true;
                            MessageBox.Show("A tinta do pincel acabou! Abra Ferramentas para recarregar.");
                        }
                    }
                    break;
            }
            lastPoint = e.Location;
            drawingPanel.Invalidate();
        }
    }

    private void StopDrawing(object sender, MouseEventArgs e)
    {
        isDrawing = false;
        isDoubleClickDrawing = false;
    }

    private void DrawingPanel_MouseMove(object sender, MouseEventArgs e)
    {
        if (previewActive && currentShape != ShapeType.None)
        {
            previewLocation = e.Location;
            drawingPanel.Invalidate();
        }
    }

    private void DrawingPanel_MouseClick(object sender, MouseEventArgs e)
    {
        if (previewActive && currentShape != ShapeType.None)
        {
            // desenha a forma na camada ativa
            canvas = layers[activeLayer];
            graphics = Graphics.FromImage(canvas);
            using (Pen pen = new Pen(currentColor, brushSize))
            {
                switch (currentShape)
                {
                    case ShapeType.Ellipse:
                        DrawEllipsePreview(graphics, e.Location);
                        break;
                    case ShapeType.Circle:
                        DrawCirclePreview(graphics, e.Location);
                        break;
                    case ShapeType.Square:
                        DrawSquarePreview(graphics, e.Location);
                        break;
                    case ShapeType.Triangle:
                        DrawTrianglePreview(graphics, e.Location);
                        break;
                }
            }
            previewActive = false;
            currentShape = ShapeType.None;
            previewLocation = Point.Empty;
            drawingPanel.Invalidate();
        }
    }

    private void ChooseColor(object sender, EventArgs e)
    {
        ColorDialog colorDialog = new ColorDialog();
        int[] customColorsInt = new int[customColors.Length];
        for (int i = 0; i < customColors.Length; i++)
            customColorsInt[i] = customColors[i].ToArgb();
        colorDialog.CustomColors = customColorsInt;

        if (colorDialog.ShowDialog() == DialogResult.OK)
        {
            currentColor = colorDialog.Color;
            for (int i = 0; i < customColors.Length; i++)
                customColors[i] = Color.FromArgb(colorDialog.CustomColors[i]);
        }
    }

    private void ClearCanvas(object sender, EventArgs e)
    {
        // limpa apenas camada ativa
        canvas = layers[activeLayer];
        graphics = Graphics.FromImage(canvas);
        graphics.Clear(Color.White);
        drawingPanel.Invalidate();
    }

    private void SaveImage(object sender, EventArgs e)
    {
        using (Form formatForm = new Form())
        {
            formatForm.Text = "Escolher formato";
            formatForm.Size = new Size(250, 120);

            Label lbl = new Label
            {
                Text = "Digite o formato (png ou jpg):",
                Location = new Point(20, 10),
                AutoSize = true
            };

            TextBox txtFormat = new TextBox
            {
                Location = new Point(20, 35),
                Width = 80,
                Text = "png"
            };

            Button btnOk = new Button
            {
                Text = "OK",
                DialogResult = DialogResult.OK,
                Location = new Point(120, 35)
            };

            formatForm.Controls.Add(lbl);
            formatForm.Controls.Add(txtFormat);
            formatForm.Controls.Add(btnOk);
            formatForm.AcceptButton = btnOk;

            if (formatForm.ShowDialog() == DialogResult.OK)
            {
                string formatStr = txtFormat.Text.Trim().ToLower();
                ImageFormat format = ImageFormat.Png;
                string ext = "png";
                if (formatStr == "jpg" || formatStr == "jpeg")
                {
                    format = ImageFormat.Jpeg;
                    ext = "jpg";
                }

                try
                {
                    string desktop = Environment.GetFolderPath(Environment.SpecialFolder.Desktop);
                    string folder = System.IO.Path.Combine(desktop, "ImagensMiniPaint");
                    if (!System.IO.Directory.Exists(folder))
                        System.IO.Directory.CreateDirectory(folder);

                    string fileName = $"MiniPaint_{DateTime.Now:yyyyMMdd_HHmmss}.{ext}";
                    string fullPath = System.IO.Path.Combine(folder, fileName);

                    // Salva todas as camadas mescladas
                    using (Bitmap merged = new Bitmap(canvas.Width, canvas.Height))
                    using (Graphics g = Graphics.FromImage(merged))
                    {
                        for (int i = 0; i < layerCount; i++)
                            g.DrawImage(layers[i], Point.Empty);
                        merged.Save(fullPath, format);
                    }

                    MessageBox.Show($"Imagem salva em:\n{fullPath}");
                }
                catch (Exception ex)
                {
                    MessageBox.Show("Erro ao salvar: " + ex.Message);
                }
            }
        }
    }

    private void ChangeSizeBrush(object sender, EventArgs e)
    {
        using (Form sizeForm = new Form())
        {
            sizeForm.Text = "Escolher tamanho do pincel";
            sizeForm.Size = new Size(250, 120);

            NumericUpDown numeric = new NumericUpDown
            {
                Minimum = 1,
                Maximum = 50,
                Value = brushSize,
                Location = new Point(20, 20),
                Width = 80
            };

            Button btnOk = new Button
            {
                Text = "OK",
                DialogResult = DialogResult.OK,
                Location = new Point(120, 20)
            };

            sizeForm.Controls.Add(numeric);
            sizeForm.Controls.Add(btnOk);
            sizeForm.AcceptButton = btnOk;

            if (sizeForm.ShowDialog() == DialogResult.OK)
            {
                brushSize = (int)numeric.Value;
            }
        }
    }

    private void DrawEllipsePreview(Graphics g, Point location)
    {
        int x = location.X - ellipseWidth / 2;
        int y = location.Y - ellipseHeight / 2;
        Rectangle rect = new Rectangle(x, y, ellipseWidth, ellipseHeight);
        g.SmoothingMode = System.Drawing.Drawing2D.SmoothingMode.AntiAlias;
        g.DrawEllipse(new Pen(currentColor, brushSize), rect);
    }

    private void DrawCirclePreview(Graphics g, Point location)
    {
        int size = Math.Min(ellipseWidth, ellipseHeight);
        int x = location.X - size / 2;
        int y = location.Y - size / 2;
        Rectangle rect = new Rectangle(x, y, size, size);
        g.SmoothingMode = System.Drawing.Drawing2D.SmoothingMode.AntiAlias;
        g.DrawEllipse(new Pen(currentColor, brushSize), rect);
    }

    private void DrawSquarePreview(Graphics g, Point location)
    {
        int size = Math.Min(ellipseWidth, ellipseHeight);
        int x = location.X - size / 2;
        int y = location.Y - size / 2;
        Rectangle rect = new Rectangle(x, y, size, size);
        g.SmoothingMode = System.Drawing.Drawing2D.SmoothingMode.AntiAlias;
        g.DrawRectangle(new Pen(currentColor, brushSize), rect);
    }

    private void DrawTrianglePreview(Graphics g, Point location)
    {
        int size = Math.Min(ellipseWidth, ellipseHeight);
        Point p1 = new Point(location.X, location.Y - size / 2);
        Point p2 = new Point(location.X - size / 2, location.Y + size / 2);
        Point p3 = new Point(location.X + size / 2, location.Y + size / 2);
        Point[] points = { p1, p2, p3 };
        g.SmoothingMode = System.Drawing.Drawing2D.SmoothingMode.AntiAlias;
        g.DrawPolygon(new Pen(currentColor, brushSize), points);
    }

    // Abre diálogo para selecionar camada
    private void ShowLayerSelector()
    {
        using (Form layerForm = new Form())
        {
            layerForm.Text = "Selecionar Camada";
            layerForm.Size = new Size(220, 140);

            ComboBox cmb = new ComboBox
            {
                Location = new Point(10, 10),
                Width = 180,
                DropDownStyle = ComboBoxStyle.DropDownList
            };
            for (int i = 0; i < layerCount; i++)
                cmb.Items.Add("Camada " + (i + 1));
            cmb.SelectedIndex = activeLayer;

            Button btnOk = new Button { Text = "OK", DialogResult = DialogResult.OK, Location = new Point(110, 50) };

            layerForm.Controls.Add(cmb);
            layerForm.Controls.Add(btnOk);
            layerForm.AcceptButton = btnOk;

            if (layerForm.ShowDialog() == DialogResult.OK)
            {
                activeLayer = cmb.SelectedIndex;
                canvas = layers[activeLayer];
                graphics = Graphics.FromImage(canvas);
                drawingPanel.Invalidate();
            }
        }
    }

    // Abre diálogo para escolher ferramenta e configurar spray / recarregar pincel
    private void ShowToolSelector()
    {
        using (Form toolForm = new Form())
        {
            toolForm.Text = "Ferramentas";
            toolForm.Size = new Size(320, 180);

            Label lblTool = new Label { Text = "Ferramenta:", Location = new Point(10, 10), AutoSize = true };
            ComboBox cmb = new ComboBox { Location = new Point(10, 30), Width = 200, DropDownStyle = ComboBoxStyle.DropDownList };
            cmb.Items.AddRange(new string[] { "Caneta", "Borracha", "Spray", "Pincel" });
            cmb.SelectedIndex = (int)currentTool;

            Label lblSpray = new Label { Text = "Densidade do spray:", Location = new Point(10, 65), AutoSize = true };
            NumericUpDown nudSpray = new NumericUpDown { Location = new Point(150, 62), Width = 60, Minimum = 1, Maximum = 500, Value = sprayDensity };

            Label lblBrush = new Label { Text = "Tinta do pincel (max):", Location = new Point(10, 95), AutoSize = true };
            NumericUpDown nudBrush = new NumericUpDown { Location = new Point(150, 92), Width = 60, Minimum = 1, Maximum = 1000, Value = brushPaintMax };

            Button btnOk = new Button { Text = "OK", DialogResult = DialogResult.OK, Location = new Point(200, 120) };

            toolForm.Controls.Add(lblTool);
            toolForm.Controls.Add(cmb);
            toolForm.Controls.Add(lblSpray);
            toolForm.Controls.Add(nudSpray);
            toolForm.Controls.Add(lblBrush);
            toolForm.Controls.Add(nudBrush);
            toolForm.Controls.Add(btnOk);
            toolForm.AcceptButton = btnOk;

            if (toolForm.ShowDialog() == DialogResult.OK)
            {
                switch (cmb.SelectedItem.ToString())
                {
                    case "Caneta": currentTool = ToolType.Pen; break;
                    case "Borracha": currentTool = ToolType.Eraser; break;
                    case "Spray": currentTool = ToolType.Spray; break;
                    case "Pincel":
                        currentTool = ToolType.Brush;
                        brushPaintMax = (int)nudBrush.Value;
                        brushPaint = brushPaintMax;
                        brushDepleted = false;
                        break;
                }
                sprayDensity = (int)nudSpray.Value;
            }
        }
    }

    private void ChooseShape()
    {
        using (Form shapeForm = new Form())
        {
            shapeForm.Text = "Escolher Forma";
            shapeForm.Size = new Size(220, 150);

            Label lbl = new Label
            {
                Text = "Selecione a forma:",
                Location = new Point(10, 10),
                AutoSize = true
            };

            ComboBox cmbShapes = new ComboBox
            {
                Location = new Point(10, 35),
                Width = 180,
                DropDownStyle = ComboBoxStyle.DropDownList
            };
            cmbShapes.Items.AddRange(new string[] { "Elipse", "Círculo", "Quadrado", "Triângulo" });
            cmbShapes.SelectedIndex = 0;

            Button btnOk = new Button
            {
                Text = "OK",
                DialogResult = DialogResult.OK,
                Location = new Point(120, 70)
            };

            shapeForm.Controls.Add(lbl);
            shapeForm.Controls.Add(cmbShapes);
            shapeForm.Controls.Add(btnOk);
            shapeForm.AcceptButton = btnOk;

            if (shapeForm.ShowDialog() == DialogResult.OK)
            {
                switch (cmbShapes.SelectedItem.ToString())
                {
                    case "Elipse":
                        currentShape = ShapeType.Ellipse;
                        break;
                    case "Círculo":
                        currentShape = ShapeType.Circle;
                        break;
                    case "Quadrado":
                        currentShape = ShapeType.Square;
                        break;
                    case "Triângulo":
                        currentShape = ShapeType.Triangle;
                        break;
                }
                previewActive = true;
            }
        }
    }

    public static void Main()
    {
        Application.Run(new MiniPaint());
    }
}

public class DoubleBufferedPanel : Panel
{
    public DoubleBufferedPanel()
    {
        this.DoubleBuffered = true;
        this.SetStyle(ControlStyles.AllPaintingInWmPaint | ControlStyles.UserPaint | ControlStyles.OptimizedDoubleBuffer, true);
        this.UpdateStyles();
    }
}