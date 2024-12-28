using Microsoft.Win32;
using System;
using System.Collections.Generic;
using System.IO;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Shapes;
using Path = System.IO.Path;

namespace ImageSelectionApp
{
    public partial class MainWindow : Window
    {
        private BitmapImage loadedImage;

        private bool isDrawing = false;
        private bool canDraw = false;

        private Point startPoint;
        private Rectangle currentRectangle;

        // Zaznaczenia: (Rectangle, nazwa, prostokąt w oryginalnych pikselach)
        private readonly List<(Rectangle Rectangle, string Name, Rect ImageRect)> selections = new();

        // Tymczasowy panel i TextBox do wpisania nazwy
        private StackPanel draftPanel;
        private TextBox draftTextBox;

        public MainWindow()
        {
            InitializeComponent();
        }

        private void LoadImageButton_Click(object sender, RoutedEventArgs e)
        {
            var ofd = new OpenFileDialog
            {
                Filter = "Obrazy (*.png;*.jpg;*.jpeg)|*.png;*.jpg;*.jpeg"
            };

            if (ofd.ShowDialog() == true)
            {
                loadedImage = new BitmapImage(new Uri(ofd.FileName));
                ImageViewer.Source = loadedImage;

                // Ustawiamy "fizyczne" wymiary w Gridzie na oryginalne wymiary bitmapy.
                // Viewbox przeskaluje to do okna.
                ImageViewer.Width = loadedImage.PixelWidth;
                ImageViewer.Height = loadedImage.PixelHeight;

                DrawingCanvas.Width = loadedImage.PixelWidth;
                DrawingCanvas.Height = loadedImage.PixelHeight;

                // Czyścimy stare zaznaczenia
                DrawingCanvas.Children.Clear();
                SelectionsPanel.Children.Clear();
                selections.Clear();

                // Na razie nie rysujemy
                canDraw = false;
                isDrawing = false;
            }
        }

        private void AddSelectionButton_Click(object sender, RoutedEventArgs e)
        {
            // Tworzymy panel z pustym TextBoxem
            draftPanel = new StackPanel
            {
                Orientation = Orientation.Horizontal,
                Margin = new Thickness(0, 0, 0, 10)
            };

            draftTextBox = new TextBox
            {
                Text = string.Empty,
                Width = 150,
                Margin = new Thickness(0, 0, 10, 0)
            };

            // Dopóki tekst jest pusty -> canDraw = false
            draftTextBox.TextChanged += (s, ea) =>
            {
                canDraw = !string.IsNullOrWhiteSpace(draftTextBox.Text);
            };

            Button removeButton = new Button
            {
                Content = "Usuń",
                Margin = new Thickness(0, 0, 10, 0)
            };
            removeButton.Click += (s, ea) =>
            {
                SelectionsPanel.Children.Remove(draftPanel);
                draftPanel = null;
                draftTextBox = null;
                canDraw = false;
            };

            draftPanel.Children.Add(draftTextBox);
            draftPanel.Children.Add(removeButton);

            SelectionsPanel.Children.Add(draftPanel);

            canDraw = false; // dopiero po wpisaniu tekstu będzie true
        }

        private void Canvas_MouseLeftButtonDown(object sender, MouseButtonEventArgs e)
        {
            if (!canDraw || loadedImage == null)
            {
                MessageBox.Show("Najpierw 'Dodaj zaznaczenie' i wpisz nazwę!",
                                "Brak nazwy", MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }

            isDrawing = true;
            // Uwaga: to są współrzędne w *przeskalowanej* przestrzeni Viewboxa
            startPoint = e.GetPosition(DrawingCanvas);

            currentRectangle = new Rectangle
            {
                Stroke = Brushes.Red,
                StrokeThickness = 2
            };
            Canvas.SetLeft(currentRectangle, startPoint.X);
            Canvas.SetTop(currentRectangle, startPoint.Y);

            DrawingCanvas.Children.Add(currentRectangle);
        }

        private void Canvas_MouseMove(object sender, MouseEventArgs e)
        {
            if (!isDrawing || currentRectangle == null) return;

            Point currentPoint = e.GetPosition(DrawingCanvas);

            double x = Math.Min(startPoint.X, currentPoint.X);
            double y = Math.Min(startPoint.Y, currentPoint.Y);
            double width = Math.Abs(startPoint.X - currentPoint.X);
            double height = Math.Abs(startPoint.Y - currentPoint.Y);

            Canvas.SetLeft(currentRectangle, x);
            Canvas.SetTop(currentRectangle, y);
            currentRectangle.Width = width;
            currentRectangle.Height = height;
        }

        private void Canvas_MouseLeftButtonUp(object sender, MouseButtonEventArgs e)
        {
            if (!isDrawing || currentRectangle == null || loadedImage == null) return;

            isDrawing = false;
            canDraw = false; // po jednym rysowaniu znów wyłączamy

            // To są współrzędne w Viewboxie (przeskalowane)
            double rectLeft = Canvas.GetLeft(currentRectangle);
            double rectTop = Canvas.GetTop(currentRectangle);
            double rectWidth = currentRectangle.Width;
            double rectHeight = currentRectangle.Height;

            // Przeliczamy na oryginalne piksele obrazu
            Rect imageRect = ConvertToImageRect(rectLeft, rectTop, rectWidth, rectHeight);

            string selectionName = draftTextBox?.Text ?? "Brak nazwy";
            selections.Add((currentRectangle, selectionName, imageRect));

            //MessageBox.Show($"Zaznaczenie „{selectionName}” dodane.", "Info",
            //                MessageBoxButton.OK, MessageBoxImage.Information);
        }

        /// <summary>
        /// Przelicza współrzędne z przeskalowanej przestrzeni (Viewbox) 
        /// na oryginalne piksele obrazu.
        /// </summary>
        private Rect ConvertToImageRect(double left, double top, double width, double height)
        {
            // Rzeczywiste (fizyczne) wymiary w Gridzie:
            double actualW = ImageViewer.Width;   // to jest PixelWidth w "nieskalowanym" Gridzie
            double actualH = ImageViewer.Height;  // to jest PixelHeight

            // Ale DrawingCanvas ma ten sam rozmiar co ImageViewer (zdefiniowane w LoadImageButton_Click).

            // Wymiary *wyświetlane* w oknie (pobrane z ActualWidth, ActualHeight)
            // to wartości przeskalowane przez Viewbox, ale my potrzebujemy oryginalnych.

            // Pobieramy faktyczny rozmiar Canvas w pikselach ekranu:
            double scaledCanvasWidth = DrawingCanvas.ActualWidth;
            double scaledCanvasHeight = DrawingCanvas.ActualHeight;

            // Obliczamy współczynnik skalowania:
            // (jak bardzo Canvas został pomniejszony/powiększony w poziomie i pionie)
            double scaleX = actualW / scaledCanvasWidth;   // ile oryginalnych pikseli przypada na 1 piksel ekranu (Canvas)
            double scaleY = actualH / scaledCanvasHeight;

            // Konwersja:
            double imageLeft = left * scaleX;
            double imageTop = top * scaleY;
            double imageWidth = width * scaleX;
            double imageHeight = height * scaleY;

            return new Rect(imageLeft, imageTop, imageWidth, imageHeight);
        }

        private void ExportSelections_Click(object sender, RoutedEventArgs e)
        {
            if (loadedImage == null || selections.Count == 0)
            {
                MessageBox.Show("Brak zaznaczeń do eksportu.",
                                "Informacja", MessageBoxButton.OK, MessageBoxImage.Information);
                return;
            }

            string exportFolder = Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments),
                "ExportedSelections");
            Directory.CreateDirectory(exportFolder);

            string csvFilePath = Path.Combine(exportFolder, "selections.csv");

            using (var csvWriter = new StreamWriter(csvFilePath))
            {
                csvWriter.WriteLine("GUID,Nazwa");

                foreach (var (rectangle, name, imageRect) in selections)
                {
                    string guid = Guid.NewGuid().ToString();
                    string imageFilePath = Path.Combine(exportFolder, $"{guid}.jpg");

                    SaveImageSection(imageRect, imageFilePath);
                    csvWriter.WriteLine($"{guid},{name}");
                }
            }

            MessageBox.Show($"Zaznaczenia wyeksportowano do:\n{exportFolder}",
                            "Eksport zakończony",
                            MessageBoxButton.OK,
                            MessageBoxImage.Information);
        }

        private void SaveImageSection(Rect imageRect, string outputPath)
        {
            // Zaokrąglamy, bo potrzebujemy całkowitych pikseli
            Int32Rect sourceRect = new Int32Rect(
                (int)Math.Round(imageRect.X),
                (int)Math.Round(imageRect.Y),
                (int)Math.Round(imageRect.Width),
                (int)Math.Round(imageRect.Height)
            );

            CroppedBitmap croppedBitmap = new CroppedBitmap(loadedImage, sourceRect);

            using FileStream fileStream = new FileStream(outputPath, FileMode.Create);
            BitmapEncoder encoder = new JpegBitmapEncoder();
            encoder.Frames.Add(BitmapFrame.Create(croppedBitmap));
            encoder.Save(fileStream);
        }
    }
}
