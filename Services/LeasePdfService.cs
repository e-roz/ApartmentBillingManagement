using Apartment.Model;
using iTextSharp.text;
using iTextSharp.text.pdf;
using Microsoft.AspNetCore.Hosting;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using System.IO;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Text;

namespace Apartment.Services
{
    public class LeasePdfService
    {
        private readonly ILogger<LeasePdfService> _logger;
        private readonly IConfiguration _configuration;
        private readonly IWebHostEnvironment _environment;
        // Define colors to avoid repeated instantiation and for consistency
        private static readonly BaseColor Black = new BaseColor(0, 0, 0);

        public LeasePdfService(ILogger<LeasePdfService> logger, IConfiguration configuration, IWebHostEnvironment environment)
        {
            _logger = logger;
            _configuration = configuration;
            _environment = environment;
        }

        public byte[]? GenerateLeasesPdf(IEnumerable<Lease> leases)
        {
            if (leases == null || !leases.Any())
            {
                _logger.LogWarning("No leases provided for PDF generation.");
                return null;
            }

            using (var memoryStream = new MemoryStream())
            {
                // 1 inch margins = 72 points (2.54 cm)
                var document = new Document(PageSize.A4, 72, 72, 72, 72);
                try
                {
                    PdfWriter.GetInstance(document, memoryStream);
                    document.Open();

                    // Load contract template
                    var contractTemplate = LoadContractTemplate();
                    if (string.IsNullOrEmpty(contractTemplate))
                    {
                        _logger.LogError("Failed to load contract template.");
                        return null;
                    }

                    // Get LandlordName from configuration
                    var landlordName = _configuration["LeaseSettings:LandlordName"] ?? "Property Management Company";

                    // Generate a contract for each lease
                    foreach (var lease in leases.OrderBy(l => l.UnitNumber).ThenBy(l => l.LeaseStart))
                    {
                        // Add page break for multiple leases (except the first one)
                        if (lease != leases.First())
                        {
                            document.NewPage();
                        }

                        // Replace placeholders with actual lease data
                        var contractContent = ReplacePlaceholders(contractTemplate, lease, landlordName);

                        // Add contract content to PDF
                        AddContractToDocument(document, contractContent);
                    }

                    document.Close();
                    return memoryStream.ToArray();
                }
                catch (DocumentException dex)
                {
                    _logger.LogError(dex, "Document error while generating lease PDF.");
                    return null;
                }
                catch (IOException ioex)
                {
                    _logger.LogError(ioex, "File access error while generating lease PDF.");
                    return null;
                }
                catch (System.Exception ex)
                {
                    _logger.LogError(ex, "Failed to generate lease PDF.");
                    return null;
                }
            }
        }

        private string LoadContractTemplate()
        {
            try
            {
                // Try multiple paths to find the contract template
                var possiblePaths = new[]
                {
                    Path.Combine(_environment.ContentRootPath, "Utilities", "contract.txt"),
                    Path.Combine(_environment.ContentRootPath, "..", "assets", "contract.txt"),
                    Path.Combine(Directory.GetCurrentDirectory(), "Utilities", "contract.txt"),
                    Path.Combine(Directory.GetCurrentDirectory(), "..", "assets", "contract.txt")
                };

                foreach (var templatePath in possiblePaths)
                {
                    if (File.Exists(templatePath))
                    {
                        _logger.LogInformation($"Loading contract template from: {templatePath}");
                        return File.ReadAllText(templatePath, Encoding.UTF8);
                    }
                }

                _logger.LogError("Contract template not found in any of the expected paths.");
                return string.Empty;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error loading contract template.");
                return string.Empty;
            }
        }

        private string ReplacePlaceholders(string template, Lease lease, string landlordName)
        {
            var culture = new CultureInfo("en-PH");
            
            return template
                .Replace("{{LandlordName}}", landlordName)
                .Replace("{{TenantName}}", lease.User?.Username ?? "N/A")
                .Replace("{{UnitName}}", lease.UnitNumber ?? "N/A")
                .Replace("{{LeaseStartDate}}", lease.LeaseStart.ToString("MMMM dd, yyyy", culture))
                .Replace("{{LeaseEndDate}}", lease.LeaseEnd.ToString("MMMM dd, yyyy", culture))
                .Replace("{{MonthlyRent}}", lease.MonthlyRent.ToString("C", culture))
                .Replace("{{LateFee}}", lease.LateFeeAmount.ToString("C", culture))
                .Replace("{{SecurityDeposit}}", lease.SecurityDeposit.ToString("C", culture))
                .Replace("{{PetsAllowed}}", lease.PetsAllowed ? "Yes" : "No");
        }

        private void AddContractToDocument(Document document, string contractContent)
        {
            // Fonts for contract styling according to contract-rule.md
            // Body Text Font: Times New Roman or Arial/Calibri, 12pt preferred
            // Main Title: ALL CAPS, Bold, Centered
            // Section Headings: Bold, ALL CAPS (or Title Case), Left-Aligned
            // Dynamic Data Fields: Bold Text, Underline text (12pt)
            var titleFont = FontFactory.GetFont(FontFactory.TIMES_ROMAN, 16, Font.BOLD, Black);
            var sectionFont = FontFactory.GetFont(FontFactory.TIMES_ROMAN, 12, Font.BOLD, Black);
            var bodyFont = FontFactory.GetFont(FontFactory.TIMES_ROMAN, 12, Font.NORMAL, Black);
            // Dynamic data fields: Bold (12pt) - underline will be applied via Chunk.SetUnderline()
            var dynamicDataFont = FontFactory.GetFont(FontFactory.TIMES_ROMAN, 12, Font.BOLD, Black);
            var signatureFont = FontFactory.GetFont(FontFactory.TIMES_ROMAN, 12, Font.NORMAL, Black);

            var lines = contractContent.Split(new[] { "\r\n", "\r", "\n" }, StringSplitOptions.None);
            
            foreach (var line in lines)
            {
                if (string.IsNullOrWhiteSpace(line))
                {
                    // Paragraph separation: full blank line between paragraphs
                    document.Add(new Paragraph(" "));
                    continue;
                }

                // Check if it's the title (Main Title: ALL CAPS, Bold, Centered)
                // The title should be just "RESIDENTIAL APARTMENT LEASE AGREEMENT" or similar
                // Not "THIS RESIDENTIAL APARTMENT LEASE AGREEMENT" which is part of the intro paragraph
                bool isTitle = (line.ToUpper().Contains("RESIDENTIAL") && line.ToUpper().Contains("LEASE AGREEMENT")) &&
                               !line.ToUpper().StartsWith("THIS") &&
                               !line.ToUpper().Contains("EXECUTED BY AND BETWEEN");
                
                if (isTitle)
                {
                    var title = new Paragraph(line.ToUpper(), titleFont)
                    {
                        Alignment = Element.ALIGN_CENTER,
                        SpacingAfter = 20f,
                        Leading = 12f // Single-spaced
                    };
                    document.Add(title);
                    continue;
                }

                // Check if it's a section header (numbered sections)
                // Section Heading: Bold, ALL CAPS (or Title Case), Left-Aligned
                // Header Separation: Double blank line before main section headings
                if (System.Text.RegularExpressions.Regex.IsMatch(line, @"^\d+\.\s+[A-Z]"))
                {
                    var section = new Paragraph(line.ToUpper(), sectionFont)
                    {
                        SpacingBefore = 24f, // Double blank line (approximately 2 * 12pt)
                        SpacingAfter = 12f,  // Full blank line after section header
                        Alignment = Element.ALIGN_LEFT,
                        Leading = 12f // Single-spaced
                    };
                    document.Add(section);
                    continue;
                }

                // Check if it's the SIGNATURES section
                if (line.ToUpper().StartsWith("SIGNATURES"))
                {
                    document.Add(new Paragraph(" ")); // Add spacing before signatures
                    var signatureHeader = new Paragraph(line.ToUpper(), sectionFont)
                    {
                        SpacingBefore = 24f, // Double blank line
                        SpacingAfter = 12f,
                        Alignment = Element.ALIGN_LEFT,
                        Leading = 12f
                    };
                    document.Add(signatureHeader);
                    continue;
                }

                // Check if it's a signature label (Landlord: or Tenant:)
                if (line.EndsWith(":") && (line.StartsWith("Landlord") || line.StartsWith("Tenant")))
                {
                    var signatureLabel = new Paragraph(line, signatureFont)
                    {
                        SpacingBefore = 12f, // Full blank line
                        SpacingAfter = 6f,
                        Alignment = Element.ALIGN_LEFT,
                        Leading = 12f
                    };
                    document.Add(signatureLabel);
                    continue;
                }

                // Check if this is part of the introductory paragraph that should not be highlighted
                // According to contract-rule.md: do not highlight the introductory paragraph
                // The text should be in normal form (not all caps, not bold)
                bool isIntroductoryParagraph = line.ToUpper().Contains("THIS RESIDENTIAL APARTMENT LEASE AGREEMENT") ||
                                               line.ToUpper().Contains("EXECUTED BY AND BETWEEN") ||
                                               line.ToUpper().Contains("HEREINAFTER REFERRED TO AS THE") ||
                                               (line.ToUpper().Contains("\"AGREEMENT\")") ||
                                               (line.ToUpper().Contains("LANDLORD") && line.ToUpper().Contains("TENANT") && 
                                                !line.Contains(":") && !line.ToUpper().StartsWith("LANDLORD:") && !line.ToUpper().StartsWith("TENANT:")));

                Phrase processedLine;
                if (isIntroductoryParagraph)
                {
                    // Don't highlight the introductory paragraph - treat as normal body text
                    // According to contract-rule.md: "do not highlight text" for the introductory paragraph
                    // Convert from all caps to normal case (title case)
                    string normalCaseLine = ConvertToNormalCase(line);
                    processedLine = new Phrase(normalCaseLine, bodyFont);
                }
                else
                {
                    // Check for dynamic data fields (e.g., Tenant Name, Monthly Rent, Lease End Date)
                    // According to contract-rule.md: Bold Text, Underline text (12pt)
                    processedLine = ProcessDynamicDataFields(line, dynamicDataFont, bodyFont);
                }

                // Regular body text
                // Alignment: Left-Aligned for all body text
                // Line Spacing: Single-spaced (12pt leading)
                // Paragraph Separation: Full blank line between paragraphs
                var paragraph = new Paragraph(processedLine)
                {
                    SpacingAfter = 12f, // Full blank line between paragraphs
                    Alignment = Element.ALIGN_LEFT, // Left-aligned
                    Leading = 12f // Single-spaced (12pt)
                };
                document.Add(paragraph);
            }
        }

        private Phrase ProcessDynamicDataFields(string line, Font boldUnderlineFont, Font normalFont)
        {
            // Create a phrase to handle mixed formatting
            var phrase = new Phrase();
            
            // Common dynamic data field label patterns that should be bold and underlined
            // According to contract-rule.md: "Dynamic Data Fields (e.g., Tenant Name, Monthly Rent, Lease End Date)"
            // Format: "Label: Value" - make the label and value bold and underlined (12pt)
            // Only match actual field labels with colons, not narrative text
            var dynamicFieldLabels = new[]
            {
                "Landlord:", "Tenant:", "Unit:", "Lease Start Date:", "Lease End Date:",
                "Monthly Rent:", "Late Fee:", "Security Deposit:", "Pets Allowed:"
            };

            // Skip if this line is part of the introductory paragraph or doesn't contain field labels
            // Only process lines that start with or contain a field label pattern at the beginning of a word
            bool foundField = false;
            int bestMatchIndex = int.MaxValue;
            string bestMatchLabel = null;

            // Find the first matching field label (must be followed by a colon)
            foreach (var label in dynamicFieldLabels)
            {
                var labelIndex = line.IndexOf(label, StringComparison.OrdinalIgnoreCase);
                if (labelIndex >= 0)
                {
                    // Verify it's actually a field label (not part of narrative text)
                    // Check if it's at the start of the line or preceded by whitespace
                    bool isFieldLabel = labelIndex == 0 || 
                                       char.IsWhiteSpace(line[labelIndex - 1]) ||
                                       line[labelIndex - 1] == '\t';
                    
                    if (isFieldLabel && labelIndex < bestMatchIndex)
                    {
                        bestMatchIndex = labelIndex;
                        bestMatchLabel = label;
                        foundField = true;
                    }
                }
            }

            if (foundField && bestMatchLabel != null)
            {
                // Add text before the label in normal font
                if (bestMatchIndex > 0)
                {
                    phrase.Add(new Chunk(line.Substring(0, bestMatchIndex), normalFont));
                }

                // Add the label and everything after it (the value) in bold and underlined
                // According to contract-rule.md: Bold Text, Underline text (12pt)
                // The value extends to the end of the line
                var fieldAndValue = line.Substring(bestMatchIndex);
                var chunk = new Chunk(fieldAndValue, boldUnderlineFont);
                chunk.SetUnderline(0.1f, -1f); // Set underline: thickness, y-position offset
                phrase.Add(chunk);
            }
            else
            {
                // If no dynamic field found, return the whole line in normal font
                phrase.Add(new Chunk(line, normalFont));
            }

            return phrase;
        }

        private string ConvertToNormalCase(string text)
        {
            // Convert all-caps text to normal case (sentence case)
            // Preserve proper nouns and specific formatting
            if (string.IsNullOrWhiteSpace(text))
                return text;

            // Check if the text is all caps (excluding punctuation and whitespace)
            bool isAllCaps = text == text.ToUpper() && text.Any(char.IsLetter);
            
            if (!isAllCaps)
                return text; // Already in normal case

            // Convert to sentence case (first letter capitalized, rest lowercase)
            // But preserve quoted terms and proper nouns
            string result = text.ToLower();
            
            // Capitalize first letter of the line
            if (result.Length > 0 && char.IsLetter(result[0]))
            {
                result = char.ToUpper(result[0]) + result.Substring(1);
            }
            
            // Preserve specific quoted terms that should remain uppercase
            result = System.Text.RegularExpressions.Regex.Replace(result, 
                @"\""agreement\""", "\"AGREEMENT\"", System.Text.RegularExpressions.RegexOptions.IgnoreCase);
            result = System.Text.RegularExpressions.Regex.Replace(result, 
                @"\""landlord\""", "\"LANDLORD\"", System.Text.RegularExpressions.RegexOptions.IgnoreCase);
            result = System.Text.RegularExpressions.Regex.Replace(result, 
                @"\""tenant\""", "\"TENANT\"", System.Text.RegularExpressions.RegexOptions.IgnoreCase);
            
            // Capitalize proper nouns that appear in the introductory paragraph
            result = System.Text.RegularExpressions.Regex.Replace(result, 
                @"\bproperty management company\b", "Property Management Company", System.Text.RegularExpressions.RegexOptions.IgnoreCase);
            
            return result;
        }
    }
}
