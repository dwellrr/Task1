using System;
using System.Diagnostics;
using System.Threading.Tasks;
using System.IO;
using OfficeOpenXml;

class JacobiMethod
{

    static void Main()
    {
        //int[] matrixSizes = { 5, 10, 100, 300, 500, 800, 1000};
        //int[] taskNumbers = { 2, 4, 8, 16, 32, 64 };
        //string excelFilePath = "JacobiTimes.xlsx";
        //RunJacobiExperiments(matrixSizes, taskNumbers, excelFilePath);


        int n = 5; 
        double[] result1 = null;
        double[] result2 = null;

        while (result1 == null)
        {
            Console.WriteLine("Generating!..");

        double[,] A = GenerateRandomMatrix(n);
        double[] b = GenerateRandomVector(n);
        double[] x0 = new double[n];

        Console.WriteLine("Generated. Solving..");

        var sequentialWatch = Stopwatch.StartNew();
        result1 = SolveJacobi(A, b, x0, 1e-6);
        sequentialWatch.Stop();
        Console.WriteLine($"Sequential execution time: {sequentialWatch.ElapsedMilliseconds} ms");

        var parallelWatch = Stopwatch.StartNew();
        result2 = SolveParallelJacobi(A, b, x0, 1e-6, 8);
        parallelWatch.Stop();
        Console.WriteLine($"Parallel execution time: {parallelWatch.ElapsedMilliseconds} ms");

            double speedup = (double)sequentialWatch.ElapsedMilliseconds / (double)parallelWatch.ElapsedMilliseconds;

            Console.WriteLine("Paralel speedup: " + speedup);

        }

        Console.WriteLine("Display results? y/n");
        string answ = Console.ReadLine();
        if (answ == "y")
        {
            Console.WriteLine("Solution concurent:");
            for (int i = 0; i < n; i++)
            {
                Console.WriteLine($"x[{i}] = {result1[i]}");
            }

            Console.WriteLine("Solution parallel:");
            for (int i = 0; i < n; i++)
            {
                Console.WriteLine($"x[{i}] = {result2[i]}");
            }
        }
        Console.ReadLine();
    }

    static void RunJacobiExperiments(int[] matrixSizes, int[] taskNumbers, string excelFilePath)
    {
        ExcelPackage.LicenseContext = LicenseContext.NonCommercial;
        using (var package = new ExcelPackage())
        {
            // Create a new Excel worksheet
            var worksheet = package.Workbook.Worksheets.Add("JacobiTimes");

            // Initialize row counter
            int currentRow = 1;

            // Iterate over matrix sizes
            foreach (var size in matrixSizes)
            {
                double[,] A = GenerateRandomMatrix(size);
                double[] b = GenerateRandomVector(size);
                double[] x0 = new double[size];
                // Run Jacobi algorithm and measure time for sequential and parallel versions
                double sequentialTime = MeasureTime(() => SolveJacobi(A, b, x0, 1e-6));

                // Iterate over task numbers
                foreach (var taskNumber in taskNumbers)
                {
                    double parallelTime = MeasureTime(() => SolveParallelJacobi(A, b, x0, 1e-6, taskNumber));

                    // Populate Excel worksheet with results
                    worksheet.Cells[currentRow, 1].Value = size;
                    worksheet.Cells[currentRow, 2].Value = taskNumber;
                    worksheet.Cells[currentRow, 3].Value = sequentialTime;
                    worksheet.Cells[currentRow, 4].Value = parallelTime;

                    // Increment row counter
                    currentRow++;
                }
            }

            // Save the Excel file
            package.SaveAs(new FileInfo(excelFilePath));
        }
    }

    static double MeasureTime(Action action)
    {
        var stopwatch = Stopwatch.StartNew();
        action.Invoke();
        stopwatch.Stop();
        return stopwatch.ElapsedMilliseconds;
    }

    static double[,] GenerateRandomMatrix(int size)
    {
        double[,] matrix = new double[size, size];
        Random random = new Random();

        for (int i = 0; i < size; i++)
        {
            // Generate random values for the row
            double[] rowValues = new double[size];
            double sum = 0;

            for (int j = 0; j < size; j++)
            {
                rowValues[j] = random.NextDouble() * 10;
                sum += Math.Abs(rowValues[j]);
            }

            // to ensure diagonal dominance
            rowValues[i] = random.NextDouble() * 10 + sum;

            for (int j = 0; j < size; j++)
            {
                matrix[i, j] = rowValues[j];
            }
        }

        return matrix;
    }

    static double[] GenerateRandomVector(int size)
    {
        double[] vector = new double[size];
        Random random = new Random();

        for (int i = 0; i < size; i++)
        {
            vector[i] = random.NextDouble() * 10; 
        }

        return vector;

    }

    static double[] SolveJacobi(double[,] A, double[] b, double[] initialGuess, double convergenceCriterion)
    {
        int n = A.GetLength(0);
        int k = 0;
        double[] x = new double[n];
        double[] xNext = new double[n];
        double error;

        // Copy the initial guess to x
        Array.Copy(initialGuess, x, n);

        do
        {
            error = 0;

            for (int i = 0; i < n; i++)
            {
                double sigma = 0;

                for (int j = 0; j < n; j++)
                {
                    if (j != i)
                    {
                        sigma += A[i, j] * x[j];
                    }
                }

                xNext[i] = (b[i] - sigma) / A[i, i];

                // Calculate the maximum error for convergence criterion
                error = Math.Max(error, Math.Abs(xNext[i] - x[i]));
            }

            // Update x for the next iteration
            Array.Copy(xNext, x, n);

            k++;
        } while (error > convergenceCriterion && error != double.PositiveInfinity);

        if (error == double.PositiveInfinity)
        {
            return null;
        }
        Console.WriteLine($"Convergence reached in {k} iterations.");

        return x;
    }

    static double[] SolveParallelJacobi(double[,] A, double[] b, double[] initialGuess, double convergenceCriterion, int numTasks)
    {
        int n = A.GetLength(0);
        int k = 0;
        double[] x = new double[n];
        double[] xNext = new double[n];
        double error;

        // Copy the initial guess to x
        Array.Copy(initialGuess, x, n);

        do
        {
            error = 0;
            Task[] tasks = new Task[numTasks];

            for (int taskIndex = 0; taskIndex < numTasks; taskIndex++)
            {
                int startRow = taskIndex * (n / numTasks);
                int endRow = (taskIndex == numTasks - 1) ? n : (taskIndex + 1) * (n / numTasks);

                tasks[taskIndex] = Task.Run(() =>
                {
                    for (int i = startRow; i < endRow; i++)
                    {
                        double sigma = 0;

                        for (int j = 0; j < n; j++)
                        {
                            if (j != i)
                            {
                                sigma += A[i, j] * x[j];
                            }
                        }

                        xNext[i] = (b[i] - sigma) / A[i, i];

                        // Calculate the maximum error for convergence criterion
                        error = Math.Max(error, Math.Abs(xNext[i] - x[i]));
                    }
                });
            }

            Task.WaitAll(tasks);

            // Update x for the next iteration
            Array.Copy(xNext, x, n);

            k++;
        } while (error > convergenceCriterion);


        if (error == double.PositiveInfinity)
        {
            return null;
        }

        Console.WriteLine($"Convergence reached in {k} iterations.");

        return x;
    }
}
