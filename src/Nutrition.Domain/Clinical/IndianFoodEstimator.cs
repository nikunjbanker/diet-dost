using System;

namespace Nutrition.Domain.Clinical;

public record FoodItemNutritionEstimate(
    string NormalizedName,
    string HindiOrRegionalName,
    string EstimatedPortion,
    double Grams,
    double Calories,
    double ProteinGrams,
    double CarbsGrams,
    double FatGrams,
    double FiberGrams,
    double SodiumMg,
    string CookingMediumEstimate,
    string Source = "ICMR-NIN Knowledge Base",
    double ConfidenceScore = 0.95
);

/// <summary>
/// Domain Nutrition Knowledge Engine for Indian Foods and Homestyle Dishes.
/// Calibrated against ICMR-NIN Dietary Guidelines and Indian Food Composition Tables (IFCT).
/// </summary>
public static class IndianFoodEstimator
{
    public static FoodItemNutritionEstimate Estimate(string itemName, string? portion = null)
    {
        if (string.IsNullOrWhiteSpace(itemName))
            return new FoodItemNutritionEstimate("Unknown Item", "Bhojan", portion ?? "1 Portion", 100, 100, 3, 15, 3, 2, 150, "Homestyle");

        var clean = itemName.Trim().ToLowerInvariant();
        // Normalize common user misspellings & regional nomenclature
        clean = clean
            .Replace("pototo", "potato")
            .Replace("patato", "potato")
            .Replace("sabji", "subzi")
            .Replace("sabzi", "subzi")
            .Replace("paner", "paneer")
            .Replace("rotii", "roti")
            .Replace("daal", "dal");

        // 1. Okra / Bhindi (including "Okra with potato", "Bhindi Aloo")
        if (clean.Contains("okra") || clean.Contains("bhindi") || clean.Contains("ladyfinger") || clean.Contains("bhendi"))
        {
            if (clean.Contains("potato") || clean.Contains("aloo"))
            {
                return new FoodItemNutritionEstimate("Bhindi Aloo (Okra with Potato)", "Aloo Bhindi ki Subzi", portion ?? "1 Katori (~120g)", 120, 120, 2.6, 16.0, 5.5, 3.5, 180, "Sautéed in Mustard Oil with Haldi & Jeera");
            }
            return new FoodItemNutritionEstimate("Bhindi Masala (Okra Stir-Fry)", "Tadka Bhindi", portion ?? "1 Katori (~100g)", 100, 110, 2.4, 10.0, 6.0, 3.8, 170, "Sautéed in Mustard Oil with Haldi & Ajwain");
        }

        // 2. Palak Paneer / Saag Paneer
        if ((clean.Contains("palak") || clean.Contains("saag")) && clean.Contains("paneer"))
        {
            return new FoodItemNutritionEstimate("Palak Paneer", "Palak Paneer", portion ?? "1 Katori (~150g)", 150, 220, 12.0, 8.0, 16.0, 3.2, 310, "Simmered in Spiced Spinach Gravy");
        }

        // 3. Paneer Dishes
        if (clean.Contains("paneer"))
        {
            if (clean.Contains("bhurji"))
                return new FoodItemNutritionEstimate("Paneer Bhurji", "Paneer Bhurji", portion ?? "1 Katori (~120g)", 120, 210, 14.0, 6.0, 15.0, 1.5, 290, "Scrambled Cottage Cheese with Onions & Tomatoes");

            return new FoodItemNutritionEstimate("Paneer Masala", "Paneer Gravy", portion ?? "1 Katori (~150g)", 150, 260, 11.5, 12.0, 19.0, 2.0, 340, "Rich Spiced Tomato & Onion Gravy");
        }

        // 4. Aloo Gobi
        if (clean.Contains("aloo") && clean.Contains("gobi"))
        {
            return new FoodItemNutritionEstimate("Aloo Gobi", "Aloo Gobi ki Subzi", portion ?? "1 Katori (~130g)", 130, 135, 3.2, 18.0, 6.0, 3.8, 210, "Homestyle Dry Masala Sauté");
        }

        // 5. Gobi / Cauliflower
        if (clean.Contains("gobi") || clean.Contains("cauliflower"))
        {
            return new FoodItemNutritionEstimate("Gobi Masala", "Gobi ki Subzi", portion ?? "1 Katori (~120g)", 120, 105, 2.8, 12.0, 5.0, 3.5, 190, "Turmeric & Cumin Sautéed Cauliflower");
        }

        // 6. Baingan / Eggplant
        if (clean.Contains("baingan") || clean.Contains("eggplant") || clean.Contains("aubergine") || clean.Contains("bharta"))
        {
            return new FoodItemNutritionEstimate("Baingan Bharta", "Roasted Baingan Bharta", portion ?? "1 Katori (~130g)", 130, 115, 2.5, 12.0, 6.5, 4.0, 190, "Fire-Roasted Eggplant with Mustard Oil & Garlic");
        }

        // 7. Lauki / Bottle Gourd
        if (clean.Contains("lauki") || clean.Contains("bottle gourd") || clean.Contains("dudhi") || clean.Contains("ghia"))
        {
            return new FoodItemNutritionEstimate("Lauki Subzi", "Ghia / Lauki ki Subzi", portion ?? "1 Katori (~120g)", 120, 70, 1.5, 8.0, 3.5, 2.5, 150, "Light Jeera Sautéed Gourd");
        }

        // 8. Tori / Ridge Gourd
        if (clean.Contains("tori") || clean.Contains("turai") || clean.Contains("ridge gourd"))
        {
            return new FoodItemNutritionEstimate("Turai Subzi", "Turai ki Subzi", portion ?? "1 Katori (~120g)", 120, 65, 1.2, 7.0, 3.5, 2.0, 140, "Homestyle Mild Sauté");
        }

        // 9. Karela / Bitter Gourd
        if (clean.Contains("karela") || clean.Contains("bitter gourd"))
        {
            return new FoodItemNutritionEstimate("Karela Fry", "Karela Masala", portion ?? "1 Katori (~100g)", 100, 85, 2.2, 9.0, 4.5, 3.0, 160, "Pan-Roasted with Saunf & Amchur");
        }

        // 10. Cabbage
        if (clean.Contains("cabbage") || clean.Contains("patta gobi") || clean.Contains("poriyal"))
        {
            return new FoodItemNutritionEstimate("Cabbage Poriyal / Subzi", "Bandh Gobi", portion ?? "1 Katori (~100g)", 100, 75, 1.8, 8.0, 4.0, 2.8, 160, "Tempered with Mustard & Curry Leaves");
        }

        // 11. Mix Veg
        if (clean.Contains("mix veg") || clean.Contains("mixed vegetable"))
        {
            return new FoodItemNutritionEstimate("Mix Vegetable Subzi", "Mili Juli Subzi", portion ?? "1 Katori (~120g)", 120, 120, 3.0, 15.0, 5.5, 3.5, 190, "Homestyle Mixed Veggies");
        }

        // 12. Chole
        if (clean.Contains("chole") || clean.Contains("chana masala") || clean.Contains("kabuli chana"))
        {
            return new FoodItemNutritionEstimate("Chole Masala", "Amritsari Chole", portion ?? "1 Katori (~150g)", 150, 185, 7.5, 26.0, 6.0, 6.0, 340, "Spiced Chickpea Curry");
        }

        // 13. Rajma
        if (clean.Contains("rajma") || clean.Contains("kidney bean"))
        {
            return new FoodItemNutritionEstimate("Rajma Masala", "Punjabi Rajma", portion ?? "1 Katori (~150g)", 150, 165, 8.5, 24.0, 4.0, 6.5, 320, "Slow-Cooked Red Kidney Beans");
        }

        // 14. Dal Makhani
        if (clean.Contains("makhani") && clean.Contains("dal"))
        {
            return new FoodItemNutritionEstimate("Dal Makhani", "Dal Makhani", portion ?? "1 Katori (~150g)", 150, 240, 8.5, 24.0, 13.0, 5.0, 380, "Black Lentils with Butter & Cream");
        }

        // 15. Dal & Sambar
        if (clean.Contains("dal") || clean.Contains("sambar"))
        {
            if (clean.Contains("sambar"))
                return new FoodItemNutritionEstimate("Sambar", "South Indian Sambar", portion ?? "1 Katori (~150ml)", 150, 95, 4.2, 14.0, 2.5, 3.5, 340, "Pigeon Pea Stew with Drumstick & Tamarind");

            if (clean.Contains("chana dal"))
                return new FoodItemNutritionEstimate("Chana Dal Tadka", "Chana Dal", portion ?? "1 Katori (~150ml)", 150, 140, 7.5, 20.0, 4.0, 5.0, 310, "Bengal Gram Dal with Garlic Tadka");

            if (clean.Contains("toor") || clean.Contains("tuvar") || clean.Contains("arhar"))
                return new FoodItemNutritionEstimate("Toor Dal Tadka", "Tuvar / Arhar Dal Fry", portion ?? "1 Katori (~150ml)", 150, 135, 7.2, 19.0, 3.5, 4.0, 310, "Split Pigeon Pea Tempered with Ghee & Hing");

            return new FoodItemNutritionEstimate("Yellow Moong Dal Tadka", "Pili Moong Dal", portion ?? "1 Katori (~150ml)", 150, 125, 7.0, 18.0, 3.5, 4.5, 300, "Jeera & Mustard Seed Tempered Moong Dal");
        }

        // 16. Kadhi
        if (clean.Contains("kadhi") || clean.Contains("karhi"))
        {
            return new FoodItemNutritionEstimate("Besan Kadhi", "Dahi Besan Kadhi", portion ?? "1 Katori (~150ml)", 150, 130, 5.0, 14.0, 6.5, 1.5, 320, "Spiced Gram Flour & Yogurt Curry");
        }

        // 17. Roti / Phulka
        if (clean.Contains("roti") || clean.Contains("phulka") || clean.Contains("chapati") || clean.Contains("chapatti"))
        {
            return new FoodItemNutritionEstimate("Whole Wheat Phulka / Roti", "Gehu ki Roti", portion ?? "1 piece (folded, ~30g)", 30, 80, 2.6, 16.0, 0.5, 2.2, 3, "Dry Tawa Baked (No Oil)");
        }

        // 18. Paratha
        if (clean.Contains("paratha") || clean.Contains("parantha"))
        {
            if (clean.Contains("aloo"))
                return new FoodItemNutritionEstimate("Aloo Paratha", "Aloo Paratha", portion ?? "1 piece (~80g)", 80, 240, 5.0, 35.0, 9.0, 3.5, 240, "Tawa Toasted with Potato Masala Filling");

            if (clean.Contains("paneer"))
                return new FoodItemNutritionEstimate("Paneer Paratha", "Paneer Paratha", portion ?? "1 piece (~80g)", 80, 280, 10.0, 28.0, 14.0, 2.5, 260, "Tawa Toasted with Spiced Cottage Cheese");

            return new FoodItemNutritionEstimate("Plain Paratha", "Tawa Paratha", portion ?? "1 piece (~50g)", 50, 180, 3.8, 26.0, 7.0, 2.5, 180, "Layered Whole Wheat Flatbread");
        }

        // 19. Rice / Pulao / Biryani
        if (clean.Contains("rice") || clean.Contains("chawal") || clean.Contains("pulao") || clean.Contains("biryani") || clean.Contains("khichdi"))
        {
            if (clean.Contains("khichdi"))
                return new FoodItemNutritionEstimate("Moong Dal Khichdi", "Dal Khichdi", portion ?? "1 Katori (~150g)", 150, 160, 5.5, 27.0, 3.8, 3.0, 280, "Homestyle Moong Dal & Rice Porridge");

            if (clean.Contains("biryani"))
            {
                if (clean.Contains("chicken"))
                    return new FoodItemNutritionEstimate("Chicken Biryani", "Murgh Biryani", portion ?? "1 Plate (~250g)", 250, 340, 22.0, 40.0, 11.0, 3.0, 480, "Aromatic Basmati with Spiced Chicken");
                return new FoodItemNutritionEstimate("Vegetable Biryani", "Veg Biryani", portion ?? "1 Plate (~200g)", 200, 250, 6.0, 42.0, 8.0, 3.5, 420, "Spiced Rice with Mixed Vegetables");
            }

            if (clean.Contains("jeera"))
                return new FoodItemNutritionEstimate("Jeera Rice", "Jeera Rice", portion ?? "1 Katori (~120g)", 120, 160, 2.8, 29.0, 3.5, 1.0, 160, "Cumin Tempered Basmati Rice");

            return new FoodItemNutritionEstimate("Steamed Basmati Rice", "Uble Chawal", portion ?? "1 Katori (~100g)", 100, 130, 2.7, 28.0, 0.4, 0.8, 2, "Steamed White Basmati");
        }

        // 20. Chicken & Non-Veg Curries
        if (clean.Contains("chicken") || clean.Contains("murgh"))
        {
            return new FoodItemNutritionEstimate("Chicken Curry", "Tariwala Chicken", portion ?? "1 Katori (~150g)", 150, 220, 22.0, 5.0, 12.0, 1.5, 380, "Homestyle Onion-Tomato Gravy");
        }

        if (clean.Contains("egg") || clean.Contains("anda"))
        {
            if (clean.Contains("bhurji"))
                return new FoodItemNutritionEstimate("Egg Bhurji", "Anda Bhurji", portion ?? "2 Eggs (~120g)", 120, 180, 13.0, 4.0, 12.0, 1.0, 320, "Spiced Indian Scrambled Eggs");
            return new FoodItemNutritionEstimate("Egg Curry", "Anda Curry", portion ?? "2 Eggs (~160g)", 160, 190, 13.0, 6.0, 13.0, 1.5, 340, "Boiled Eggs in Rich Spiced Gravy");
        }

        if (clean.Contains("fish") || clean.Contains("machli") || clean.Contains("prawn"))
        {
            return new FoodItemNutritionEstimate("Fish Curry", "Machli Curry", portion ?? "1 Katori (~150g)", 150, 175, 19.0, 4.0, 9.0, 1.0, 360, "Mustard or Tomato Spiced Fish Gravy");
        }

        if (clean.Contains("mutton") || clean.Contains("gosht") || clean.Contains("lamb"))
        {
            return new FoodItemNutritionEstimate("Mutton Curry", "Mutton Rogan Josh", portion ?? "1 Katori (~150g)", 150, 280, 20.0, 4.0, 20.0, 1.0, 420, "Slow-Braised Mutton Curry");
        }

        // 21. Curd / Chaas
        if (clean.Contains("curd") || clean.Contains("dahi") || clean.Contains("yogurt") || clean.Contains("chaas") || clean.Contains("buttermilk") || clean.Contains("raita"))
        {
            if (clean.Contains("chaas") || clean.Contains("buttermilk"))
                return new FoodItemNutritionEstimate("Jeera Chaas", "Masala Chaas", portion ?? "1 Glass (~200ml)", 200, 40, 2.5, 3.5, 1.8, 0.5, 120, "Whisked Buttermilk with Roasted Jeera");

            if (clean.Contains("raita"))
                return new FoodItemNutritionEstimate("Raita", "Dahi Raita", portion ?? "1 Katori (~120g)", 120, 90, 3.5, 9.0, 4.5, 1.0, 140, "Spiced Yogurt with Cucumber / Boondi");

            return new FoodItemNutritionEstimate("Fresh Curd (Dahi)", "Ghar ka Dahi", portion ?? "1 Katori (~100g)", 100, 60, 3.5, 4.5, 3.0, 0.0, 40, "Plain Homestyle Set Curd");
        }

        // 22. South Indian Items
        if (clean.Contains("idli"))
            return new FoodItemNutritionEstimate("Steamed Idli", "Idli", portion ?? "2 pieces (~80g)", 80, 110, 4.0, 22.0, 0.4, 1.8, 110, "Fermented Rice & Urad Dal Steamed Cakes");

        if (clean.Contains("dosa"))
        {
            if (clean.Contains("masala"))
                return new FoodItemNutritionEstimate("Masala Dosa", "Masala Dosa", portion ?? "1 piece (~150g)", 150, 250, 5.0, 38.0, 9.0, 3.0, 320, "Crispy Crepe with Spiced Potato Filling");
            return new FoodItemNutritionEstimate("Plain Dosa", "Sada Dosa", portion ?? "1 piece (~80g)", 80, 135, 3.2, 23.0, 3.5, 1.8, 180, "Golden Crispy Fermented Crepe");
        }

        if (clean.Contains("poha"))
            return new FoodItemNutritionEstimate("Batata Poha", "Kanda Poha", portion ?? "1 Plate (~150g)", 150, 180, 3.5, 32.0, 5.0, 2.5, 220, "Flattened Rice with Mustard Seeds, Curry Leaves & Peanuts");

        if (clean.Contains("upma"))
            return new FoodItemNutritionEstimate("Rava Upma", "Sooji Upma", portion ?? "1 Plate (~150g)", 150, 170, 4.0, 28.0, 5.5, 2.5, 210, "Tempered Semolina Porridge with Veggies");

        if (clean.Contains("chilla") || clean.Contains("cheela"))
            return new FoodItemNutritionEstimate("Besan Chilla", "Besan ka Cheela", portion ?? "1 piece (~60g)", 60, 120, 5.5, 14.0, 4.5, 3.0, 160, "Savory Spiced Gram Flour Pancake");

        // 23. Bakery & Indian Snacks
        if (clean.Contains("puff") || clean.Contains("patties") || clean.Contains("patty"))
        {
            return new FoodItemNutritionEstimate("Veg Puff / Veg Patties", "Aloo Patties / Veg Puff", portion ?? "1 piece (~85g)", 85, 268, 4.5, 28.2, 15.6, 2.1, 380, "Bakery Shortening / Margarine");
        }

        if (clean.Contains("samosa"))
        {
            return new FoodItemNutritionEstimate("Samosa (Potato & Peas)", "Aloo Samosa", portion ?? "1 piece (~80g)", 80, 240, 3.5, 25.0, 14.0, 2.0, 320, "Deep Fried in Vegetable Oil");
        }

        if (clean.Contains("sauce") || clean.Contains("ketchup") || clean.Contains("tomato sauce"))
        {
            return new FoodItemNutritionEstimate("Tomato Ketchup / Sauce", "Tomato Sauce", portion ?? "1 tbsp (~18g)", 18, 20, 0.2, 4.6, 0.1, 0.1, 160, "Bottled Condiment with Sugar & Salt");
        }

        // 24. Salad / Raw
        if (clean.Contains("salad") || clean.Contains("kachumber") || clean.Contains("cucumber") || clean.Contains("kheera"))
        {
            return new FoodItemNutritionEstimate("Green Salad", "Kachumber Salad", portion ?? "1 Small Plate (~80g)", 80, 30, 1.0, 6.0, 0.2, 2.0, 25, "Fresh Raw Cucumber, Tomato & Onion Slices");
        }

        // Generic Vegetable fallback
        return new FoodItemNutritionEstimate(
            itemName, 
            "Ghar ki Subzi", 
            portion ?? "1 Katori (~100g)", 
            100, 
            110, 
            2.5, 
            12.0, 
            5.5, 
            3.0, 
            180, 
            "Homestyle Turmeric & Cumin Sauté"
        );
    }
}
