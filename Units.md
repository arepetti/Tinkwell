# UnitsNet Supported Units

This document lists all the quantity types and their corresponding units of measurement supported by the UnitsNet library.

## Table of Contents

- [Basic Dimensions](#basic-dimensions)
  - [Amount Of Substance](#amount-of-substance)
  - [Angle](#angle)
  - [Area](#area)
  - [Duration](#duration)
  - [Information](#information)
  - [Length](#length)
  - [Mass](#mass)
  - [Scalar](#scalar)
  - [Solid Angle](#solid-angle)
  - [Volume](#volume)
- [Mechanics](#mechanics)
  - [Acceleration](#acceleration)
  - [Brake Specific Fuel Consumption](#brake-specific-fuel-consumption)
  - [Compressibility](#compressibility)
  - [Density](#density)
  - [Dynamic Viscosity](#dynamic-viscosity)
  - [Force](#force)
  - [Force Change Rate](#force-change-rate)
  - [Force Per Length](#force-per-length)
  - [Fuel Efficiency](#fuel-efficiency)
  - [Impulse](#impulse)
  - [Jerk](#jerk)
  - [Kinematic Viscosity](#kinematic-viscosity)
  - [Linear Density](#linear-density)
  - [Mass Flow](#mass-flow)
  - [Mass Flux](#mass-flux)
  - [Mass Fraction](#mass-fraction)
  - [Mass Moment Of Inertia](#mass-moment-of-inertia)
  - [Pressure](#pressure)
  - [Pressure Change Rate](#pressure-change-rate)
  - [Rotational Acceleration](#rotational-acceleration)
  - [Rotational Speed](#rotational-speed)
  - [Rotational Stiffness](#rotational-stiffness)
  - [Rotational Stiffness Per Length](#rotational-stiffness-per-length)
  - [Specific Fuel Consumption](#specific-fuel-consumption)
  - [Specific Volume](#specific-volume)
  - [Specific Weight](#specific-weight)
  - [Speed](#speed)
  - [Standard Volume Flow](#standard-volume-flow)
  - [Torque](#torque)
  - [Torque Per Length](#torque-per-length)
  - [Volume Flow](#volume-flow)
  - [Volume Flow Per Area](#volume-flow-per-area)
  - [Volume Per Length](#volume-per-length)
  - [Warping Moment Of Inertia](#warping-moment-of-inertia)
- [Electrical & Magnetic](#electrical-&-magnetic)
  - [Electric Admittance](#electric-admittance)
  - [Electric Apparent Energy](#electric-apparent-energy)
  - [Electric Apparent Power](#electric-apparent-power)
  - [Electric Capacitance](#electric-capacitance)
  - [Electric Charge](#electric-charge)
  - [Electric Charge Density](#electric-charge-density)
  - [Electric Conductance](#electric-conductance)
  - [Electric Conductivity](#electric-conductivity)
  - [Electric Current](#electric-current)
  - [Electric Current Density](#electric-current-density)
  - [Electric Current Gradient](#electric-current-gradient)
  - [Electric Field](#electric-field)
  - [Electric Impedance](#electric-impedance)
  - [Electric Inductance](#electric-inductance)
  - [Electric Potential](#electric-potential)
  - [Electric Potential Change Rate](#electric-potential-change-rate)
  - [Electric Reactance](#electric-reactance)
  - [Electric Reactive Energy](#electric-reactive-energy)
  - [Electric Reactive Power](#electric-reactive-power)
  - [Electric Resistance](#electric-resistance)
  - [Electric Resistivity](#electric-resistivity)
  - [Electric Surface Charge Density](#electric-surface-charge-density)
  - [Electric Susceptance](#electric-susceptance)
  - [Magnetic Field](#magnetic-field)
  - [Magnetic Flux](#magnetic-flux)
  - [Magnetization](#magnetization)
  - [Permeability](#permeability)
  - [Permittivity](#permittivity)
- [Thermal](#thermal)
  - [Coefficient Of Thermal Expansion](#coefficient-of-thermal-expansion)
  - [Energy](#energy)
  - [Energy Density](#energy-density)
  - [Entropy](#entropy)
  - [Heat Flux](#heat-flux)
  - [Heat Transfer Coefficient](#heat-transfer-coefficient)
  - [Molar Energy](#molar-energy)
  - [Molar Entropy](#molar-entropy)
  - [Specific Energy](#specific-energy)
  - [Specific Entropy](#specific-entropy)
  - [Temperature](#temperature)
  - [Temperature Change Rate](#temperature-change-rate)
  - [Temperature Delta](#temperature-delta)
  - [Temperature Gradient](#temperature-gradient)
  - [Thermal Conductivity](#thermal-conductivity)
  - [Thermal Resistance](#thermal-resistance)
  - [Volumetric Heat Capacity](#volumetric-heat-capacity)
- [Light & Radiation](#light-&-radiation)
  - [Absorbed Dose Of Ionizing Radiation](#absorbed-dose-of-ionizing-radiation)
  - [Dose Area Product](#dose-area-product)
  - [Illuminance](#illuminance)
  - [Irradiance](#irradiance)
  - [Irradiation](#irradiation)
  - [Luminance](#luminance)
  - [Luminosity](#luminosity)
  - [Luminous Flux](#luminous-flux)
  - [Luminous Intensity](#luminous-intensity)
  - [Radiation Equivalent Dose](#radiation-equivalent-dose)
  - [Radiation Equivalent Dose Rate](#radiation-equivalent-dose-rate)
  - [Radiation Exposure](#radiation-exposure)
  - [Radioactivity](#radioactivity)
  - [Vitamin A](#vitamin-a)
- [Chemical & Material](#chemical-&-material)
  - [Mass Concentration](#mass-concentration)
  - [Molality](#molality)
  - [Molarity](#molarity)
  - [Porous Medium Permeability](#porous-medium-permeability)
  - [Turbidity](#turbidity)
  - [Volume Concentration](#volume-concentration)
- [Ratios & Levels](#ratios-&-levels)
  - [Amplitude Ratio](#amplitude-ratio)
  - [Level](#level)
  - [Ratio](#ratio)
  - [Ratio Change Rate](#ratio-change-rate)
  - [Relative Humidity](#relative-humidity)
- [Other](#other)
  - [Area Density](#area-density)
  - [Area Moment Of Inertia](#area-moment-of-inertia)
  - [Bit Rate](#bit-rate)
  - [Fluid Resistance](#fluid-resistance)
  - [Frequency](#frequency)
  - [Lapse Rate](#lapse-rate)
  - [Leak Rate](#leak-rate)
  - [Linear Power Density](#linear-power-density)
  - [Molar Flow](#molar-flow)
  - [Molar Mass](#molar-mass)
  - [Power](#power)
  - [Power Density](#power-density)
  - [Power Ratio](#power-ratio)
  - [Reciprocal Area](#reciprocal-area)
  - [Reciprocal Length](#reciprocal-length)
  - [Thermal Insulance](#thermal-insulance)

## Basic Dimensions

### Amount Of Substance
`AmountOfSubstance`

Mole is the amount of substance containing Avagadro's Number (6.02 x 10 ^ 23) of real particles such as molecules,atoms, ions or radicals.

**Base Unit**: Mole

**Units**:

- Mole (`Mole`). Units: mol
- Pound Mole (`PoundMole`). Units: lbmol

### Angle
`Angle`

In geometry, an angle is the figure formed by two rays, called the sides of the angle, sharing a common endpoint, called the vertex of the angle.

**Base Unit**: Radian

**Units**:

- Radian (`Radian`). Units: rad
- Degree (`Degree`). Units: °, deg
- Arcminute (`Arcminute`). Units: ', arcmin, amin, min
- Arcsecond (`Arcsecond`). Units: ″, arcsec, asec, sec
- Gradian (`Gradian`). Units: g
- Nato Mil (`NatoMil`). Units: mil
- Revolution (`Revolution`). Units: r

### Area
`Area`

Area is a quantity that expresses the extent of a two-dimensional surface or shape, or planar lamina, in the plane. Area can be understood as the amount of material with a given thickness that would be necessary to fashion a model of the shape, or the amount of paint necessary to cover the surface with a single coat.[1] It is the two-dimensional analog of the length of a curve (a one-dimensional concept) or the volume of a solid (a three-dimensional concept).

**Base Unit**: SquareMeter

**Units**:

- Square Kilometer (`SquareKilometer`). Units: km²
- Square Meter (`SquareMeter`). Units: m²
- Square Decimeter (`SquareDecimeter`). Units: dm²
- Square Centimeter (`SquareCentimeter`). Units: cm²
- Square Millimeter (`SquareMillimeter`). Units: mm²
- Square Micrometer (`SquareMicrometer`). Units: µm²
- Square Mile (`SquareMile`). Units: mi²: The statute mile was standardised between the British Commonwealth and the United States by an international agreement in 1959, when it was formally redefined with respect to SI units as exactly 1,609.344 metres.
- Square Yard (`SquareYard`). Units: yd²: The yard (symbol: yd) is an English unit of length in both the British imperial and US customary systems of measurement equalling 3 feet (or 36 inches). Since 1959 the yard has been by international agreement standardized as exactly 0.9144 meter. A distance of 1,760 yards is equal to 1 mile.
- Square Foot (`SquareFoot`). Units: ft²
- Us Survey Square Foot (`UsSurveySquareFoot`). Units: ft² (US): In the United States, the foot was defined as 12 inches, with the inch being defined by the Mendenhall Order of 1893 as 39.37 inches = 1 m. This makes a U.S. survey foot exactly 1200/3937 meters.
- Square Inch (`SquareInch`). Units: in²
- Acre (`Acre`). Units: ac: Based upon the international yard and pound agreement of 1959, an acre may be declared as exactly 4,046.8564224 square metres.
- Hectare (`Hectare`). Units: ha
- Square Nautical Mile (`SquareNauticalMile`). Units: nmi²

### Duration
`Duration`

Time is a dimension in which events can be ordered from the past through the present into the future, and also the measure of durations of events and the intervals between them.

**Base Unit**: Second

**Units**:

- Year365 (`Year365`). Units: yr, year, years
- Month30 (`Month30`). Units: mo, month, months
- Week (`Week`). Units: wk, week, weeks
- Day (`Day`). Units: d, day, days
- Hour (`Hour`). Units: h, hr, hrs, hour, hours
- Minute (`Minute`). Units: m, min, minute, minutes
- Second (`Second`). Units: s, sec, secs, second, seconds
- Julian Year (`JulianYear`). Units: jyr, jyear, jyears
- Sol (`Sol`). Units: sol

### Information
`Information`

In computing and telecommunications, a unit of information is the capacity of some standard data storage system or communication channel, used to measure the capacities of other systems and channels. In information theory, units of information are also used to measure the information contents or entropy of random variables.

**Base Unit**: Bit

**Units**:

- Byte (`Byte`). Units: B
- Octet (`Octet`). Units: o
- Bit (`Bit`). Units: b

### Length
`Length`

Many different units of length have been used around the world. The main units in modern use are U.S. customary units in the United States and the Metric system elsewhere. British Imperial units are still used for some purposes in the United Kingdom and some other countries. The metric system is sub-divided into SI and non-SI units.

**Base Unit**: Meter

**Units**:

- Meter (`Meter`). Units: m
- Mile (`Mile`). Units: mi: The statute mile was standardised between the British Commonwealth and the United States by an international agreement in 1959, when it was formally redefined with respect to SI units as exactly 1,609.344 metres.
- Yard (`Yard`). Units: yd: The yard (symbol: yd) is an English unit of length in both the British imperial and US customary systems of measurement equalling 3 feet (or 36 inches). Since 1959 the yard has been by international agreement standardized as exactly 0.9144 meter. A distance of 1,760 yards is equal to 1 mile.
- Foot (`Foot`). Units: ft, ', ′: The foot (pl. feet; standard symbol: ft) is a unit of length in the British imperial and United States customary systems of measurement. The prime symbol, ′, is commonly used to represent the foot. In both customary and imperial units, one foot comprises 12 inches, and one yard comprises three feet. Since an international agreement in 1959, the foot is defined as equal to exactly 0.3048 meters.
- Us Survey Foot (`UsSurveyFoot`). Units: ftUS: In the United States, the foot was defined as 12 inches, with the inch being defined by the Mendenhall Order of 1893 as 39.37 inches = 1 m. This makes a U.S. survey foot exactly 1200/3937 meters.
- Inch (`Inch`). Units: in, \", ″: The inch (symbol: in or ″) is a unit of length in the British Imperial and the United States customary systems of measurement. It is equal to 1/36 yard or 1/12 of a foot. Derived from the Roman uncia ("twelfth"), the word inch is also sometimes used to translate similar units in other measurement systems, usually understood as deriving from the width of the human thumb.
- Mil (`Mil`). Units: mil
- Nautical Mile (`NauticalMile`). Units: NM, nmi
- Fathom (`Fathom`). Units: fathom
- Shackle (`Shackle`). Units: shackle
- Microinch (`Microinch`). Units: µin
- Printer Point (`PrinterPoint`). Units: pt: In typography, the point is the smallest unit of measure. It is used for measuring font size, leading, and other items on a printed page. In modern times this size of the point has been approximated as exactly 1⁄72.27 (0.01383700013837) of the inch by Donald Knuth for the default unit of his TeX computer typesetting system and is thus sometimes known as the TeX point.
- Dtp Point (`DtpPoint`). Units: pt: The desktop publishing point (DTP) is defined as 1⁄72 of an inch (1/72 × 25.4 mm ≈ 0.353 mm) and, as with earlier American point sizes, is considered to be 1⁄12 of a pica.
- Printer Pica (`PrinterPica`). Units: pica: The American pica of 0.16604 inches (~4.217 mm) was established by the United States Type Founders' Association in 1886. In TeX one pica is 400⁄2409 of an inch.
- Dtp Pica (`DtpPica`). Units: pica: The pica is a typographic unit of measure corresponding to approximately 1⁄6 of an inch, or from 1⁄68 to 1⁄73 of a foot. One pica is further divided into 12 points.
- Twip (`Twip`). Units: twip: A twip (abbreviating "twentieth of a point" or "twentieth of an inch point") is a typographical measurement, defined as 1⁄20 of a typographical point. One twip is 1⁄1440 inch, or ~17.64 μm.
- Hand (`Hand`). Units: h, hh: The hand is a non-SI unit of measurement of length standardized to 4 in (101.6 mm). It is used to measure the height of horses in many English-speaking countries, including Australia, Canada, Ireland, the United Kingdom, and the United States. It was originally based on the breadth of a human hand.
- Astronomical Unit (`AstronomicalUnit`). Units: au, ua: One Astronomical Unit is the distance from the solar system Star, the sun, to planet Earth.
- Parsec (`Parsec`). Units: pc: A parsec is defined as the distance at which one astronomical unit (AU) subtends an angle of one arcsecond.
- Light Year (`LightYear`). Units: ly: A Light Year (ly) is the distance that light travel during an Earth year, ie 365 days.
- Solar Radius (`SolarRadius`). Units: R⊙: Solar radius is a ratio unit to the radius of the solar system star, the sun.
- Chain (`Chain`). Units: ch: The chain (abbreviated ch) is a unit of length equal to 66 feet (22 yards), used in both the US customary and Imperial unit systems. It is subdivided into 100 links. There are 10 chains in a furlong, and 80 chains in one statute mile. In metric terms, it is 20.1168 m long.
- Angstrom (`Angstrom`). Units: Å, A: Angstrom is a metric unit of length equal to 1e-10 meter
- Data Mile (`DataMile`). Units: DM: In radar-related subjects and in JTIDS, a data mile is a unit of distance equal to 6000 feet (1.8288 kilometres or 0.987 nautical miles).

### Mass
`Mass`

In physics, mass (from Greek μᾶζα "barley cake, lump [of dough]") is a property of a physical system or body, giving rise to the phenomena of the body's resistance to being accelerated by a force and the strength of its mutual gravitational attraction with other bodies. Instruments such as mass balances or scales use those phenomena to measure mass. The SI unit of mass is the kilogram (kg).

**Base Unit**: Kilogram

**Units**:

- Gram (`Gram`). Units: g
- Tonne (`Tonne`). Units: t: The tonne is a unit of mass equal to 1,000 kilograms. It is a non-SI unit accepted for use with SI. It is also referred to as a metric ton in the United States to distinguish it from the non-metric units of the short ton (United States customary units) and the long ton (British imperial units). It is equivalent to approximately 2,204.6 pounds, 1.102 short tons, and 0.984 long tons.
- Short Ton (`ShortTon`). Units: t (short), short tn, ST: The short ton is a unit of mass equal to 2,000 pounds (907.18474 kg), that is most commonly used in the United States – known there simply as the ton.
- Long Ton (`LongTon`). Units: long tn: Long ton (weight ton or Imperial ton) is a unit of mass equal to 2,240 pounds (1,016 kg) and is the name for the unit called the "ton" in the avoirdupois or Imperial system of measurements that was used in the United Kingdom and several other Commonwealth countries before metrication.
- Pound (`Pound`). Units: lb, lbs, lbm: The pound or pound-mass (abbreviations: lb, lbm) is a unit of mass used in the imperial, United States customary and other systems of measurement. A number of different definitions have been used, the most common today being the international avoirdupois pound which is legally defined as exactly 0.45359237 kilograms, and which is divided into 16 avoirdupois ounces.
- Ounce (`Ounce`). Units: oz: The international avoirdupois ounce (abbreviated oz) is defined as exactly 28.349523125 g under the international yard and pound agreement of 1959, signed by the United States and countries of the Commonwealth of Nations. 16 oz make up an avoirdupois pound.
- Slug (`Slug`). Units: slug: The slug (abbreviation slug) is a unit of mass that is accelerated by 1 ft/s² when a force of one pound (lbf) is exerted on it.
- Stone (`Stone`). Units: st: The stone (abbreviation st) is a unit of mass equal to 14 pounds avoirdupois (about 6.35 kilograms) used in Great Britain and Ireland for measuring human body weight.
- Short Hundredweight (`ShortHundredweight`). Units: cwt: The short hundredweight (abbreviation cwt) is a unit of mass equal to 100 pounds in US and Canada. In British English, the short hundredweight is referred to as the "cental".
- Long Hundredweight (`LongHundredweight`). Units: cwt: The long or imperial hundredweight (abbreviation cwt) is a unit of mass equal to 112 pounds in US and Canada.
- Grain (`Grain`). Units: gr: A grain is a unit of measurement of mass, and in the troy weight, avoirdupois, and Apothecaries' system, equal to exactly 64.79891 milligrams.
- Solar Mass (`SolarMass`). Units: M☉, M⊙: Solar mass is a ratio unit to the mass of the solar system star, the sun.
- Earth Mass (`EarthMass`). Units: em: Earth mass is a ratio unit to the mass of planet Earth.

### Scalar
`Scalar`

A way of representing a number of items.

**Base Unit**: Amount

**Units**:

- Amount (`Amount`)

### Solid Angle
`SolidAngle`

In geometry, a solid angle is the two-dimensional angle in three-dimensional space that an object subtends at a point.

**Base Unit**: Steradian

**Units**:

- Steradian (`Steradian`). Units: sr

### Volume
`Volume`

Volume is the quantity of three-dimensional space enclosed by some closed boundary, for example, the space that a substance (solid, liquid, gas, or plasma) or shape occupies or contains.[1] Volume is often quantified numerically using the SI derived unit, the cubic metre. The volume of a container is generally understood to be the capacity of the container, i. e. the amount of fluid (gas or liquid) that the container could hold, rather than the amount of space the container itself displaces.

**Base Unit**: CubicMeter

**Units**:

- Liter (`Liter`). Units: l
- Cubic Meter (`CubicMeter`). Units: m³
- Cubic Kilometer (`CubicKilometer`). Units: km³
- Cubic Hectometer (`CubicHectometer`). Units: hm³
- Cubic Decimeter (`CubicDecimeter`). Units: dm³
- Cubic Centimeter (`CubicCentimeter`). Units: cm³
- Cubic Millimeter (`CubicMillimeter`). Units: mm³
- Cubic Micrometer (`CubicMicrometer`). Units: µm³
- Cubic Mile (`CubicMile`). Units: mi³: A cubic mile (abbreviation: cu mi or mi3) is an imperial and US customary (non-SI non-metric) unit of volume, used in the United States, Canada and the United Kingdom. It is defined as the volume of a cube with sides of 1 mile (63360 inches, 5280 feet, 1760 yards or ~1.609 kilometres) in length.
- Cubic Yard (`CubicYard`). Units: yd³: A cubic yard is an Imperial / U.S. customary (non-SI non-metric) unit of volume, used in Canada and the United States. It is defined as the volume of a cube with sides of 1 yard (3 feet, 36 inches, 0.9144 meters) in length.
- Cubic Foot (`CubicFoot`). Units: ft³: The cubic foot (symbol ft3 or cu ft) is an imperial and US customary (non-metric) unit of volume, used in the United States and the United Kingdom. It is defined as the volume of a cube with sides of one foot (0.3048 m) in length.
- Cubic Inch (`CubicInch`). Units: in³: The cubic inch (symbol in3) is a unit of volume in the Imperial units and United States customary units systems. It is the volume of a cube with each of its three dimensions (length, width, and height) being one inch long which is equivalent to 1/231 of a US gallon.
- Imperial Gallon (`ImperialGallon`). Units: gal (imp.): The British imperial gallon (frequently called simply "gallon") is defined as exactly 4.54609 litres.
- Imperial Ounce (`ImperialOunce`). Units: oz (imp.): An imperial fluid ounce is 1⁄20 of an imperial pint, 1⁄160 of an imperial gallon or exactly 28.4130625 mL.
- Us Gallon (`UsGallon`). Units: gal (U.S.): The US liquid gallon (frequently called simply "gallon") is legally defined as 231 cubic inches, which is exactly 3.785411784 litres.
- Us Ounce (`UsOunce`). Units: oz (U.S.): A US customary fluid ounce is 1⁄16 of a US liquid pint and 1⁄128 of a US liquid gallon or exactly 29.5735295625 mL, making it about 4.08% larger than the imperial fluid ounce.
- Us Tablespoon (`UsTablespoon`). Units: tablespoon (U.S.): The traditional U.S. interpretation of the tablespoon as a unit of volume is: 1 US tablespoon = 4 fluid drams, or 3 teaspoons or 1/2 US fluid ounce (≈ 14.8 ml)
- Au Tablespoon (`AuTablespoon`). Units: tablespoon (A.U.): In Australia, the definition of the tablespoon is 20 ml (0.70 imp fl oz).
- Uk Tablespoon (`UkTablespoon`). Units: tablespoon (U.K.): In nutrition labeling in the U.S. and the U.K., a tablespoon is defined as 15 ml (0.51 US fl oz). In Australia, the definition of the tablespoon is 20 ml (0.70 imp fl oz).
- Metric Teaspoon (`MetricTeaspoon`). Units: tsp, t, ts, tspn, t., ts., tsp., tspn., teaspoon: The metric teaspoon as a unit of culinary measure is 5 ml (0.18 imp fl oz; 0.17 US fl oz),[17] equal to 5 cm3, 1⁄3 UK/Canadian metric tablespoon, or 1⁄4 Australian metric tablespoon.
- Us Teaspoon (`UsTeaspoon`). Units: teaspoon (U.S.): As a unit of culinary measure, one teaspoon in the United States is 1⁄3 tablespoon, exactly 4.92892159375 ml, 1 1⁄3 US fluid drams, 1⁄6 US fl oz, 1⁄48 US cup, 1⁄768 US liquid gallon, or 77⁄256 (0.30078125) cubic inches.
- Metric Cup (`MetricCup`). Units: metric cup: Australia, Canada, New Zealand, and some other members of the Commonwealth of Nations, being former British colonies that have since metricated, employ a metric cup of 250 millilitres. Although derived from the metric system, it is not an SI unit.
- Us Customary Cup (`UsCustomaryCup`). Units: cup (U.S. customary): In the United States, the customary cup is half of a liquid pint or 1⁄16 US customary gallon which is 236.5882365 milliliters exactly.
- Us Legal Cup (`UsLegalCup`). Units: cup (U.S.): The cup currently used in the United States for nutrition labelling is defined in United States law as 240 ml.
- Oil Barrel (`OilBarrel`). Units: bbl: In the oil industry, one barrel (unit symbol bbl) is a unit of volume used for measuring oil defined as exactly 42 US gallons, approximately 159 liters, or 35 imperial gallons.
- Us Beer Barrel (`UsBeerBarrel`). Units: bl (U.S.): Fluid barrels vary depending on what is being measured and where. In the US most fluid barrels (apart from oil) are 31.5 US gallons (26 imp gal; 119 L) (half a hogshead), but a beer barrel is 31 US gallons (26 imp gal; 117 L).
- Imperial Beer Barrel (`ImperialBeerBarrel`). Units: bl (imp.): Fluid barrels vary depending on what is being measured and where. In the UK a beer barrel is 36 imperial gallons (43 US gal; ~164 L).
- Us Quart (`UsQuart`). Units: qt (U.S.): The US liquid quart equals 57.75 cubic inches, which is exactly equal to 0.946352946 L.
- Imperial Quart (`ImperialQuart`). Units: qt (imp.): The imperial quart, which is used for both liquid and dry capacity, is equal to one quarter of an imperial gallon, or exactly 1.1365225 liters.
- Us Pint (`UsPint`). Units: pt (U.S.): The pint is a unit of volume or capacity in both the imperial and United States customary measurement systems. In both of those systems it is traditionally one eighth of a gallon. The British imperial pint is about 20% larger than the American pint because the two systems are defined differently.
- Acre Foot (`AcreFoot`). Units: ac-ft, acre-foot, acre-feet: An acre-foot is 43,560 cubic feet (~1,233.5 m3).
- Imperial Pint (`ImperialPint`). Units: pt (imp.), UK pt, pt, p: The pint is a unit of volume or capacity in both the imperial and United States customary measurement systems. In both of those systems it is traditionally one eighth of a gallon. The British imperial pint is about 20% larger than the American pint because the two systems are defined differently.
- Board Foot (`BoardFoot`). Units: bf, board foot, board feet: The board foot or board-foot is a unit of measurement for the volume of lumber in the United States and Canada. It equals the volume of a board that is one-foot (305 mm) in length, one-foot (305 mm) in width, and one-inch (25.4 mm) in thickness.

## Mechanics

### Acceleration
`Acceleration`

Acceleration, in physics, is the rate at which the velocity of an object changes over time. An object's acceleration is the net result of any and all forces acting on the object, as described by Newton's Second Law. The SI unit for acceleration is the Meter per second squared (m/s²). Accelerations are vector quantities (they have magnitude and direction) and add according to the parallelogram law. As a vector, the calculated net force is equal to the product of the object's mass (a scalar quantity) and the acceleration.

**Base Unit**: MeterPerSecondSquared

**Units**:

- Meter Per Second Squared (`MeterPerSecondSquared`). Units: m/s²
- Inch Per Second Squared (`InchPerSecondSquared`). Units: in/s²
- Foot Per Second Squared (`FootPerSecondSquared`). Units: ft/s²
- Knot Per Second (`KnotPerSecond`). Units: kn/s
- Knot Per Minute (`KnotPerMinute`). Units: kn/min: The knot (/nɒt/) is a unit of speed equal to one nautical mile per hour, exactly 1.852 km/h (approximately 1.151 mph or 0.514 m/s).
- Knot Per Hour (`KnotPerHour`). Units: kn/h: The knot (/nɒt/) is a unit of speed equal to one nautical mile per hour, exactly 1.852 km/h (approximately 1.151 mph or 0.514 m/s).
- Standard Gravity (`StandardGravity`). Units: g

### Brake Specific Fuel Consumption
`BrakeSpecificFuelConsumption`

Brake specific fuel consumption (BSFC) is a measure of the fuel efficiency of any prime mover that burns fuel and produces rotational, or shaft, power. It is typically used for comparing the efficiency of internal combustion engines with a shaft output.

**Base Unit**: KilogramPerJoule

**Units**:

- Gram Per Kilo Watt Hour (`GramPerKiloWattHour`). Units: g/kWh
- Kilogram Per Joule (`KilogramPerJoule`). Units: kg/J
- Pound Per Mechanical Horsepower Hour (`PoundPerMechanicalHorsepowerHour`). Units: lb/hph: The pound per horse power hour uses mechanical horse power and the imperial pound

### Compressibility
`Compressibility`

**Base Unit**: InversePascal

**Units**:

- Inverse Pascal (`InversePascal`). Units: Pa⁻¹, 1/Pa
- Inverse Kilopascal (`InverseKilopascal`). Units: kPa⁻¹, 1/kPa
- Inverse Megapascal (`InverseMegapascal`). Units: MPa⁻¹, 1/MPa
- Inverse Atmosphere (`InverseAtmosphere`). Units: atm⁻¹, 1/atm
- Inverse Millibar (`InverseMillibar`). Units: mbar⁻¹, 1/mbar
- Inverse Bar (`InverseBar`). Units: bar⁻¹, 1/bar
- Inverse Pound Force Per Square Inch (`InversePoundForcePerSquareInch`). Units: psi⁻¹, 1/psi

### Density
`Density`

The density, or more precisely, the volumetric mass density, of a substance is its mass per unit volume.

**Base Unit**: KilogramPerCubicMeter

**Units**:

- Gram Per Cubic Millimeter (`GramPerCubicMillimeter`). Units: g/mm³
- Gram Per Cubic Centimeter (`GramPerCubicCentimeter`). Units: g/cm³
- Gram Per Cubic Meter (`GramPerCubicMeter`). Units: g/m³
- Pound Per Cubic Inch (`PoundPerCubicInch`). Units: lb/in³, lbm/in³
- Pound Per Cubic Foot (`PoundPerCubicFoot`). Units: lb/ft³, lbm/ft³
- Pound Per Cubic Yard (`PoundPerCubicYard`). Units: lb/yd³, lbm/yd³: Calculated from the definition of <a href="https://en.wikipedia.org/wiki/Pound_(mass)">pound</a> and <a href="https://en.wikipedia.org/wiki/Cubic_yard">Cubic yard</a> compared to metric kilogram and meter.
- Tonne Per Cubic Millimeter (`TonnePerCubicMillimeter`). Units: t/mm³
- Tonne Per Cubic Centimeter (`TonnePerCubicCentimeter`). Units: t/cm³
- Tonne Per Cubic Meter (`TonnePerCubicMeter`). Units: t/m³
- Slug Per Cubic Foot (`SlugPerCubicFoot`). Units: slug/ft³
- Gram Per Liter (`GramPerLiter`). Units: g/l
- Gram Per Deciliter (`GramPerDeciliter`). Units: g/dl
- Gram Per Milliliter (`GramPerMilliliter`). Units: g/ml
- Pound Per U S Gallon (`PoundPerUSGallon`). Units: ppg (U.S.)
- Pound Per Imperial Gallon (`PoundPerImperialGallon`). Units: ppg (imp.)
- Kilogram Per Liter (`KilogramPerLiter`). Units: kg/l
- Tonne Per Cubic Foot (`TonnePerCubicFoot`). Units: t/ft³
- Tonne Per Cubic Inch (`TonnePerCubicInch`). Units: t/in³
- Gram Per Cubic Foot (`GramPerCubicFoot`). Units: g/ft³
- Gram Per Cubic Inch (`GramPerCubicInch`). Units: g/in³
- Pound Per Cubic Meter (`PoundPerCubicMeter`). Units: lb/m³, lbm/m³
- Pound Per Cubic Centimeter (`PoundPerCubicCentimeter`). Units: lb/cm³, lbm/cm³
- Pound Per Cubic Millimeter (`PoundPerCubicMillimeter`). Units: lb/mm³, lbm/mm³
- Slug Per Cubic Meter (`SlugPerCubicMeter`). Units: slug/m³
- Slug Per Cubic Centimeter (`SlugPerCubicCentimeter`). Units: slug/cm³
- Slug Per Cubic Millimeter (`SlugPerCubicMillimeter`). Units: slug/mm³
- Slug Per Cubic Inch (`SlugPerCubicInch`). Units: slug/in³

### Dynamic Viscosity
`DynamicViscosity`

The dynamic (shear) viscosity of a fluid expresses its resistance to shearing flows, where adjacent layers move parallel to each other with different speeds

**Base Unit**: NewtonSecondPerMeterSquared

**Units**:

- Newton Second Per Meter Squared (`NewtonSecondPerMeterSquared`). Units: Ns/m²
- Pascal Second (`PascalSecond`). Units: Pa·s, PaS
- Poise (`Poise`). Units: P
- Reyn (`Reyn`). Units: reyn
- Pound Force Second Per Square Inch (`PoundForceSecondPerSquareInch`). Units: lbf·s/in²
- Pound Force Second Per Square Foot (`PoundForceSecondPerSquareFoot`). Units: lbf·s/ft²
- Pound Per Foot Second (`PoundPerFootSecond`). Units: lb/(ft·s)

### Force
`Force`

In physics, a force is any influence that causes an object to undergo a certain change, either concerning its movement, direction, or geometrical construction. In other words, a force can cause an object with mass to change its velocity (which includes to begin moving from a state of rest), i.e., to accelerate, or a flexible object to deform, or both. Force can also be described by intuitive concepts such as a push or a pull. A force has both magnitude and direction, making it a vector quantity. It is measured in the SI unit of newtons and represented by the symbol F.

**Base Unit**: Newton

**Units**:

- Dyn (`Dyn`). Units: dyn: One dyne is equal to 10 micronewtons, 10e−5 N or to 10 nsn (nanosthenes) in the old metre–tonne–second system of units.
- Kilogram Force (`KilogramForce`). Units: kgf: The kilogram-force, or kilopond, is equal to the magnitude of the force exerted on one kilogram of mass in a 9.80665 m/s2 gravitational field (standard gravity). Therefore, one kilogram-force is by definition equal to 9.80665 N.
- Tonne Force (`TonneForce`). Units: tf, Ton: The tonne-force, metric ton-force, megagram-force, and megapond (Mp) are each 1000 kilograms-force.
- Newton (`Newton`). Units: N: The newton (symbol: N) is the unit of force in the International System of Units (SI). It is defined as 1 kg⋅m/s2, the force which gives a mass of 1 kilogram an acceleration of 1 metre per second per second.
- Kilo Pond (`KiloPond`). Units: kp: The kilogram-force, or kilopond, is equal to the magnitude of the force exerted on one kilogram of mass in a 9.80665 m/s2 gravitational field (standard gravity). Therefore, one kilogram-force is by definition equal to 9.80665 N.
- Poundal (`Poundal`). Units: pdl: The poundal is defined as the force necessary to accelerate 1 pound-mass at 1 foot per second per second. 1 pdl = 0.138254954376 N exactly.
- Pound Force (`PoundForce`). Units: lbf: The standard values of acceleration of the standard gravitational field (gn) and the international avoirdupois pound (lb) result in a pound-force equal to 4.4482216152605 N.
- Ounce Force (`OunceForce`). Units: ozf: An ounce-force is 1⁄16 of a pound-force, or about 0.2780139 newtons.
- Short Ton Force (`ShortTonForce`). Units: tf (short), t (US)f, short tons-force: The short ton-force is a unit of force equal to 2,000 pounds-force (907.18474 kgf), that is most commonly used in the United States – known there simply as the ton or US ton.

### Force Change Rate
`ForceChangeRate`

Force change rate is the ratio of the force change to the time during which the change occurred (value of force changes per unit time).

**Base Unit**: NewtonPerSecond

**Units**:

- Newton Per Minute (`NewtonPerMinute`). Units: N/min
- Newton Per Second (`NewtonPerSecond`). Units: N/s
- Pound Force Per Minute (`PoundForcePerMinute`). Units: lbf/min
- Pound Force Per Second (`PoundForcePerSecond`). Units: lbf/s

### Force Per Length
`ForcePerLength`

The magnitude of force per unit length.

**Base Unit**: NewtonPerMeter

**Units**:

- Newton Per Meter (`NewtonPerMeter`). Units: N/m
- Newton Per Centimeter (`NewtonPerCentimeter`). Units: N/cm
- Newton Per Millimeter (`NewtonPerMillimeter`). Units: N/mm
- Kilogram Force Per Meter (`KilogramForcePerMeter`). Units: kgf/m
- Kilogram Force Per Centimeter (`KilogramForcePerCentimeter`). Units: kgf/cm
- Kilogram Force Per Millimeter (`KilogramForcePerMillimeter`). Units: kgf/mm
- Tonne Force Per Meter (`TonneForcePerMeter`). Units: tf/m
- Tonne Force Per Centimeter (`TonneForcePerCentimeter`). Units: tf/cm
- Tonne Force Per Millimeter (`TonneForcePerMillimeter`). Units: tf/mm
- Pound Force Per Foot (`PoundForcePerFoot`). Units: lbf/ft
- Pound Force Per Inch (`PoundForcePerInch`). Units: lbf/in
- Pound Force Per Yard (`PoundForcePerYard`). Units: lbf/yd
- Kilopound Force Per Foot (`KilopoundForcePerFoot`). Units: kipf/ft, kip/ft, k/ft
- Kilopound Force Per Inch (`KilopoundForcePerInch`). Units: kipf/in, kip/in, k/in

### Fuel Efficiency
`FuelEfficiency`

In the context of transport, fuel economy is the energy efficiency of a particular vehicle, given as a ratio of distance traveled per unit of fuel consumed. In most countries, using the metric system, fuel economy is stated as "fuel consumption" in liters per 100 kilometers (L/100 km) or kilometers per liter (km/L or kmpl). In countries using non-metric system, fuel economy is expressed in miles per gallon (mpg) (imperial galon or US galon).

**Base Unit**: KilometerPerLiter

**Units**:

- Liter Per100 Kilometers (`LiterPer100Kilometers`). Units: l/100km
- Mile Per Us Gallon (`MilePerUsGallon`). Units: mpg (U.S.)
- Mile Per Uk Gallon (`MilePerUkGallon`). Units: mpg (imp.)
- Kilometer Per Liter (`KilometerPerLiter`). Units: km/l

### Impulse
`Impulse`

In classical mechanics, impulse is the integral of a force, F, over the time interval, t, for which it acts. Impulse applied to an object produces an equivalent vector change in its linear momentum, also in the resultant direction.

**Base Unit**: NewtonSecond

**Units**:

- Kilogram Meter Per Second (`KilogramMeterPerSecond`). Units: kg·m/s
- Newton Second (`NewtonSecond`). Units: N·s
- Pound Foot Per Second (`PoundFootPerSecond`). Units: lb·ft/s
- Pound Force Second (`PoundForceSecond`). Units: lbf·s
- Slug Foot Per Second (`SlugFootPerSecond`). Units: slug·ft/s

### Jerk
`Jerk`

**Base Unit**: MeterPerSecondCubed

**Units**:

- Meter Per Second Cubed (`MeterPerSecondCubed`). Units: m/s³
- Inch Per Second Cubed (`InchPerSecondCubed`). Units: in/s³
- Foot Per Second Cubed (`FootPerSecondCubed`). Units: ft/s³
- Standard Gravities Per Second (`StandardGravitiesPerSecond`). Units: g/s

### Kinematic Viscosity
`KinematicViscosity`

The viscosity of a fluid is a measure of its resistance to gradual deformation by shear stress or tensile stress.

**Base Unit**: SquareMeterPerSecond

**Units**:

- Square Meter Per Second (`SquareMeterPerSecond`). Units: m²/s
- Stokes (`Stokes`). Units: St
- Square Foot Per Second (`SquareFootPerSecond`). Units: ft²/s

### Linear Density
`LinearDensity`

The Linear Density, or more precisely, the linear mass density, of a substance is its mass per unit length.  The term linear density is most often used when describing the characteristics of one-dimensional objects, although linear density can also be used to describe the density of a three-dimensional quantity along one particular dimension.

**Base Unit**: KilogramPerMeter

**Units**:

- Gram Per Millimeter (`GramPerMillimeter`). Units: g/mm
- Gram Per Centimeter (`GramPerCentimeter`). Units: g/cm
- Gram Per Meter (`GramPerMeter`). Units: g/m
- Pound Per Inch (`PoundPerInch`). Units: lb/in
- Pound Per Foot (`PoundPerFoot`). Units: lb/ft
- Gram Per Foot (`GramPerFoot`). Units: g/ft

### Mass Flow
`MassFlow`

Mass flow is the ratio of the mass change to the time during which the change occurred (value of mass changes per unit time).

**Base Unit**: GramPerSecond

**Units**:

- Gram Per Second (`GramPerSecond`). Units: g/s, g/S
- Gram Per Day (`GramPerDay`). Units: g/d
- Gram Per Hour (`GramPerHour`). Units: g/h
- Kilogram Per Hour (`KilogramPerHour`). Units: kg/h
- Kilogram Per Minute (`KilogramPerMinute`). Units: kg/min
- Tonne Per Hour (`TonnePerHour`). Units: t/h
- Pound Per Day (`PoundPerDay`). Units: lb/d
- Pound Per Hour (`PoundPerHour`). Units: lb/h
- Pound Per Minute (`PoundPerMinute`). Units: lb/min
- Pound Per Second (`PoundPerSecond`). Units: lb/s
- Tonne Per Day (`TonnePerDay`). Units: t/d
- Short Ton Per Hour (`ShortTonPerHour`). Units: short tn/h

### Mass Flux
`MassFlux`

Mass flux is the mass flow rate per unit area.

**Base Unit**: KilogramPerSecondPerSquareMeter

**Units**:

- Gram Per Second Per Square Meter (`GramPerSecondPerSquareMeter`). Units: g·s⁻¹·m⁻²
- Gram Per Second Per Square Centimeter (`GramPerSecondPerSquareCentimeter`). Units: g·s⁻¹·cm⁻²
- Gram Per Second Per Square Millimeter (`GramPerSecondPerSquareMillimeter`). Units: g·s⁻¹·mm⁻²
- Gram Per Hour Per Square Meter (`GramPerHourPerSquareMeter`). Units: g·h⁻¹·m⁻²
- Gram Per Hour Per Square Centimeter (`GramPerHourPerSquareCentimeter`). Units: g·h⁻¹·cm⁻²
- Gram Per Hour Per Square Millimeter (`GramPerHourPerSquareMillimeter`). Units: g·h⁻¹·mm⁻²

### Mass Fraction
`MassFraction`

The mass fraction is defined as the mass of a constituent divided by the total mass of the mixture.

**Base Unit**: DecimalFraction

**Units**:

- Decimal Fraction (`DecimalFraction`)
- Gram Per Gram (`GramPerGram`). Units: g/g
- Gram Per Kilogram (`GramPerKilogram`). Units: g/kg
- Percent (`Percent`). Units: %, % (w/w)
- Part Per Thousand (`PartPerThousand`). Units: ‰
- Part Per Million (`PartPerMillion`). Units: ppm
- Part Per Billion (`PartPerBillion`). Units: ppb
- Part Per Trillion (`PartPerTrillion`). Units: ppt

### Mass Moment Of Inertia
`MassMomentOfInertia`

A property of body reflects how its mass is distributed with regard to an axis.

**Base Unit**: KilogramSquareMeter

**Units**:

- Gram Square Meter (`GramSquareMeter`). Units: g·m²
- Gram Square Decimeter (`GramSquareDecimeter`). Units: g·dm²
- Gram Square Centimeter (`GramSquareCentimeter`). Units: g·cm²
- Gram Square Millimeter (`GramSquareMillimeter`). Units: g·mm²
- Tonne Square Meter (`TonneSquareMeter`). Units: t·m²
- Tonne Square Decimeter (`TonneSquareDecimeter`). Units: t·dm²
- Tonne Square Centimeter (`TonneSquareCentimeter`). Units: t·cm²
- Tonne Square Millimeter (`TonneSquareMillimeter`). Units: t·mm²
- Pound Square Foot (`PoundSquareFoot`). Units: lb·ft²
- Pound Square Inch (`PoundSquareInch`). Units: lb·in²
- Slug Square Foot (`SlugSquareFoot`). Units: slug·ft²
- Slug Square Inch (`SlugSquareInch`). Units: slug·in²

### Pressure
`Pressure`

Pressure (symbol: P or p) is the ratio of force to the area over which that force is distributed. Pressure is force per unit area applied in a direction perpendicular to the surface of an object. Gauge pressure (also spelled gage pressure)[a] is the pressure relative to the local atmospheric or ambient pressure. Pressure is measured in any unit of force divided by any unit of area. The SI unit of pressure is the newton per square metre, which is called the pascal (Pa) after the seventeenth-century philosopher and scientist Blaise Pascal. A pressure of 1 Pa is small; it approximately equals the pressure exerted by a dollar bill resting flat on a table. Everyday pressures are often stated in kilopascals (1 kPa = 1000 Pa).

**Base Unit**: Pascal

**Units**:

- Pascal (`Pascal`). Units: Pa
- Atmosphere (`Atmosphere`). Units: atm: The standard atmosphere (symbol: atm) is a unit of pressure defined as 101325 Pa. It is sometimes used as a reference pressure or standard pressure. It is approximately equal to Earth's average atmospheric pressure at sea level.
- Bar (`Bar`). Units: bar: The bar is a metric unit of pressure defined as 100,000 Pa (100 kPa), though not part of the International System of Units (SI). A pressure of 1 bar is slightly less than the current average atmospheric pressure on Earth at sea level (approximately 1.013 bar).
- Kilogram Force Per Square Meter (`KilogramForcePerSquareMeter`). Units: kgf/m²
- Kilogram Force Per Square Centimeter (`KilogramForcePerSquareCentimeter`). Units: kgf/cm²: A kilogram-force per centimetre square (kgf/cm2), often just kilogram per square centimetre (kg/cm2), or kilopond per centimetre square (kp/cm2) is a deprecated unit of pressure using metric units. It is not a part of the International System of Units (SI), the modern metric system. 1 kgf/cm2 equals 98.0665 kPa (kilopascals). It is also known as a technical atmosphere (symbol: at).
- Kilogram Force Per Square Millimeter (`KilogramForcePerSquareMillimeter`). Units: kgf/mm²
- Newton Per Square Meter (`NewtonPerSquareMeter`). Units: N/m²
- Newton Per Square Centimeter (`NewtonPerSquareCentimeter`). Units: N/cm²
- Newton Per Square Millimeter (`NewtonPerSquareMillimeter`). Units: N/mm²
- Technical Atmosphere (`TechnicalAtmosphere`). Units: at: A kilogram-force per centimetre square (kgf/cm2), often just kilogram per square centimetre (kg/cm2), or kilopond per centimetre square (kp/cm2) is a deprecated unit of pressure using metric units. It is not a part of the International System of Units (SI), the modern metric system. 1 kgf/cm2 equals 98.0665 kPa (kilopascals). It is also known as a technical atmosphere (symbol: at).
- Torr (`Torr`). Units: torr: The torr (symbol: Torr) is a unit of pressure based on an absolute scale, defined as exactly 1/760 of a standard atmosphere (101325 Pa). Thus one torr is exactly 101325/760 pascals (≈ 133.32 Pa).
- Pound Force Per Square Inch (`PoundForcePerSquareInch`). Units: psi, lb/in²
- Pound Force Per Square Mil (`PoundForcePerSquareMil`). Units: lb/mil², lbs/mil²
- Pound Force Per Square Foot (`PoundForcePerSquareFoot`). Units: lb/ft²
- Tonne Force Per Square Millimeter (`TonneForcePerSquareMillimeter`). Units: tf/mm²
- Tonne Force Per Square Meter (`TonneForcePerSquareMeter`). Units: tf/m²
- Meter Of Head (`MeterOfHead`). Units: m of head
- Tonne Force Per Square Centimeter (`TonneForcePerSquareCentimeter`). Units: tf/cm²
- Foot Of Head (`FootOfHead`). Units: ft of head
- Millimeter Of Mercury (`MillimeterOfMercury`). Units: mmHg: A millimetre of mercury is a manometric unit of pressure, formerly defined as the extra pressure generated by a column of mercury one millimetre high, and currently defined as exactly 133.322387415 pascals.
- Inch Of Mercury (`InchOfMercury`). Units: inHg: Inch of mercury (inHg and ″Hg) is a non-SI unit of measurement for pressure. It is used for barometric pressure in weather reports, refrigeration and aviation in the United States. It is the pressure exerted by a column of mercury 1 inch (25.4 mm) in height at the standard acceleration of gravity.
- Dyne Per Square Centimeter (`DynePerSquareCentimeter`). Units: dyn/cm²
- Pound Per Inch Second Squared (`PoundPerInchSecondSquared`). Units: lbm/(in·s²), lb/(in·s²)
- Meter Of Water Column (`MeterOfWaterColumn`). Units: mH₂O, mH2O, m wc, m wg: A centimetre of water is defined as the pressure exerted by a column of water of 1 cm in height at 4 °C (temperature of maximum density) at the standard acceleration of gravity, so that 1 cmH2O (4°C) = 999.9720 kg/m3 × 9.80665 m/s2 × 1 cm = 98.063754138 Pa, but conventionally a nominal maximum water density of 1000 kg/m3 is used, giving 98.0665 Pa.
- Inch Of Water Column (`InchOfWaterColumn`). Units: inH2O, inch wc, wc: Inches of water is a non-SI unit for pressure. It is defined as the pressure exerted by a column of water of 1 inch in height at defined conditions. At a temperature of 4 °C (39.2 °F) pure water has its highest density (1000 kg/m3). At that temperature and assuming the standard acceleration of gravity, 1 inAq is approximately 249.082 pascals (0.0361263 psi).

### Pressure Change Rate
`PressureChangeRate`

Pressure change rate is the ratio of the pressure change to the time during which the change occurred (value of pressure changes per unit time).

**Base Unit**: PascalPerSecond

**Units**:

- Pascal Per Second (`PascalPerSecond`). Units: Pa/s
- Pascal Per Minute (`PascalPerMinute`). Units: Pa/min
- Millimeter Of Mercury Per Second (`MillimeterOfMercuryPerSecond`). Units: mmHg/s
- Atmosphere Per Second (`AtmospherePerSecond`). Units: atm/s
- Pound Force Per Square Inch Per Second (`PoundForcePerSquareInchPerSecond`). Units: psi/s, lb/in²/s
- Pound Force Per Square Inch Per Minute (`PoundForcePerSquareInchPerMinute`). Units: psi/min, lb/in²/min
- Bar Per Second (`BarPerSecond`). Units: bar/s
- Bar Per Minute (`BarPerMinute`). Units: bar/min

### Rotational Acceleration
`RotationalAcceleration`

Angular acceleration is the rate of change of rotational speed.

**Base Unit**: RadianPerSecondSquared

**Units**:

- Radian Per Second Squared (`RadianPerSecondSquared`). Units: rad/s²
- Degree Per Second Squared (`DegreePerSecondSquared`). Units: °/s², deg/s²
- Revolution Per Minute Per Second (`RevolutionPerMinutePerSecond`). Units: rpm/s
- Revolution Per Second Squared (`RevolutionPerSecondSquared`). Units: r/s²

### Rotational Speed
`RotationalSpeed`

Rotational speed (sometimes called speed of revolution) is the number of complete rotations, revolutions, cycles, or turns per time unit. Rotational speed is a cyclic frequency, measured in radians per second or in hertz in the SI System by scientists, or in revolutions per minute (rpm or min-1) or revolutions per second in everyday life. The symbol for rotational speed is ω (the Greek lowercase letter "omega").

**Base Unit**: RadianPerSecond

**Units**:

- Radian Per Second (`RadianPerSecond`). Units: rad/s
- Degree Per Second (`DegreePerSecond`). Units: °/s, deg/s
- Degree Per Minute (`DegreePerMinute`). Units: °/min, deg/min
- Revolution Per Second (`RevolutionPerSecond`). Units: r/s
- Revolution Per Minute (`RevolutionPerMinute`). Units: rpm, r/min

### Rotational Stiffness
`RotationalStiffness`

https://en.wikipedia.org/wiki/Stiffness#Rotational_stiffness

**Base Unit**: NewtonMeterPerRadian

**Units**:

- Newton Meter Per Radian (`NewtonMeterPerRadian`). Units: N·m/rad, Nm/rad
- Pound Force Foot Per Degrees (`PoundForceFootPerDegrees`). Units: lbf·ft/deg
- Kilopound Force Foot Per Degrees (`KilopoundForceFootPerDegrees`). Units: kipf·ft/°, kip·ft/°g, k·ft/°, kipf·ft/deg, kip·ft/deg, k·ft/deg
- Newton Millimeter Per Degree (`NewtonMillimeterPerDegree`). Units: N·mm/deg, Nmm/deg, N·mm/°, Nmm/°
- Newton Meter Per Degree (`NewtonMeterPerDegree`). Units: N·m/deg, Nm/deg, N·m/°, Nm/°
- Newton Millimeter Per Radian (`NewtonMillimeterPerRadian`). Units: N·mm/rad, Nmm/rad
- Pound Force Feet Per Radian (`PoundForceFeetPerRadian`). Units: lbf·ft/rad

### Rotational Stiffness Per Length
`RotationalStiffnessPerLength`

https://en.wikipedia.org/wiki/Stiffness#Rotational_stiffness

**Base Unit**: NewtonMeterPerRadianPerMeter

**Units**:

- Newton Meter Per Radian Per Meter (`NewtonMeterPerRadianPerMeter`). Units: N·m/rad/m, Nm/rad/m
- Pound Force Foot Per Degrees Per Foot (`PoundForceFootPerDegreesPerFoot`). Units: lbf·ft/deg/ft
- Kilopound Force Foot Per Degrees Per Foot (`KilopoundForceFootPerDegreesPerFoot`). Units: kipf·ft/°/ft, kip·ft/°/ft, k·ft/°/ft, kipf·ft/deg/ft, kip·ft/deg/ft, k·ft/deg/ft

### Specific Fuel Consumption
`SpecificFuelConsumption`

SFC is the fuel efficiency of an engine design with respect to thrust output

**Base Unit**: GramPerKilonewtonSecond

**Units**:

- Pound Mass Per Pound Force Hour (`PoundMassPerPoundForceHour`). Units: lb/(lbf·h)
- Kilogram Per Kilogram Force Hour (`KilogramPerKilogramForceHour`). Units: kg/(kgf·h)
- Gram Per Kilonewton Second (`GramPerKilonewtonSecond`). Units: g/(kN·s)

### Specific Volume
`SpecificVolume`

In thermodynamics, the specific volume of a substance is the ratio of the substance's volume to its mass. It is the reciprocal of density and an intrinsic property of matter as well.

**Base Unit**: CubicMeterPerKilogram

**Units**:

- Cubic Meter Per Kilogram (`CubicMeterPerKilogram`). Units: m³/kg
- Cubic Foot Per Pound (`CubicFootPerPound`). Units: ft³/lb

### Specific Weight
`SpecificWeight`

The SpecificWeight, or more precisely, the volumetric weight density, of a substance is its weight per unit volume.

**Base Unit**: NewtonPerCubicMeter

**Units**:

- Newton Per Cubic Millimeter (`NewtonPerCubicMillimeter`). Units: N/mm³
- Newton Per Cubic Centimeter (`NewtonPerCubicCentimeter`). Units: N/cm³
- Newton Per Cubic Meter (`NewtonPerCubicMeter`). Units: N/m³
- Kilogram Force Per Cubic Millimeter (`KilogramForcePerCubicMillimeter`). Units: kgf/mm³
- Kilogram Force Per Cubic Centimeter (`KilogramForcePerCubicCentimeter`). Units: kgf/cm³
- Kilogram Force Per Cubic Meter (`KilogramForcePerCubicMeter`). Units: kgf/m³
- Pound Force Per Cubic Inch (`PoundForcePerCubicInch`). Units: lbf/in³
- Pound Force Per Cubic Foot (`PoundForcePerCubicFoot`). Units: lbf/ft³
- Tonne Force Per Cubic Millimeter (`TonneForcePerCubicMillimeter`). Units: tf/mm³
- Tonne Force Per Cubic Centimeter (`TonneForcePerCubicCentimeter`). Units: tf/cm³
- Tonne Force Per Cubic Meter (`TonneForcePerCubicMeter`). Units: tf/m³

### Speed
`Speed`

In everyday use and in kinematics, the speed of an object is the magnitude of its velocity (the rate of change of its position); it is thus a scalar quantity.[1] The average speed of an object in an interval of time is the distance travelled by the object divided by the duration of the interval;[2] the instantaneous speed is the limit of the average speed as the duration of the time interval approaches zero.

**Base Unit**: MeterPerSecond

**Units**:

- Meter Per Second (`MeterPerSecond`). Units: m/s
- Meter Per Minute (`MeterPerMinute`). Units: m/min
- Meter Per Hour (`MeterPerHour`). Units: m/h
- Foot Per Second (`FootPerSecond`). Units: ft/s
- Foot Per Minute (`FootPerMinute`). Units: ft/min
- Foot Per Hour (`FootPerHour`). Units: ft/h
- Us Survey Foot Per Second (`UsSurveyFootPerSecond`). Units: ftUS/s
- Us Survey Foot Per Minute (`UsSurveyFootPerMinute`). Units: ftUS/min
- Us Survey Foot Per Hour (`UsSurveyFootPerHour`). Units: ftUS/h
- Inch Per Second (`InchPerSecond`). Units: in/s
- Inch Per Minute (`InchPerMinute`). Units: in/min
- Inch Per Hour (`InchPerHour`). Units: in/h
- Yard Per Second (`YardPerSecond`). Units: yd/s
- Yard Per Minute (`YardPerMinute`). Units: yd/min
- Yard Per Hour (`YardPerHour`). Units: yd/h
- Knot (`Knot`). Units: kn, kt, knot, knots: The knot, by definition, is a unit of speed equals to 1 nautical mile per hour, which is exactly 1852.000 metres per hour. The length of the internationally agreed nautical mile is 1852 m. The US adopted the international definition in 1954, the UK adopted the international nautical mile definition in 1970.
- Mile Per Hour (`MilePerHour`). Units: mph
- Mach (`Mach`). Units: M, Ma, MN, MACH

### Standard Volume Flow
`StandardVolumeFlow`

The molar flow rate of a gas corrected to standardized conditions of temperature and pressure thus representing a fixed number of moles of gas regardless of composition and actual flow conditions.

**Base Unit**: StandardCubicMeterPerSecond

**Units**:

- Standard Cubic Meter Per Second (`StandardCubicMeterPerSecond`). Units: Sm³/s
- Standard Cubic Meter Per Minute (`StandardCubicMeterPerMinute`). Units: Sm³/min
- Standard Cubic Meter Per Hour (`StandardCubicMeterPerHour`). Units: Sm³/h
- Standard Cubic Meter Per Day (`StandardCubicMeterPerDay`). Units: Sm³/d
- Standard Cubic Centimeter Per Minute (`StandardCubicCentimeterPerMinute`). Units: sccm
- Standard Liter Per Minute (`StandardLiterPerMinute`). Units: slm
- Standard Cubic Foot Per Second (`StandardCubicFootPerSecond`). Units: Sft³/s
- Standard Cubic Foot Per Minute (`StandardCubicFootPerMinute`). Units: scfm
- Standard Cubic Foot Per Hour (`StandardCubicFootPerHour`). Units: scfh

### Torque
`Torque`

Torque, moment or moment of force (see the terminology below), is the tendency of a force to rotate an object about an axis,[1] fulcrum, or pivot. Just as a force is a push or a pull, a torque can be thought of as a twist to an object. Mathematically, torque is defined as the cross product of the lever-arm distance and force, which tends to produce rotation. Loosely speaking, torque is a measure of the turning force on an object such as a bolt or a flywheel. For example, pushing or pulling the handle of a wrench connected to a nut or bolt produces a torque (turning force) that loosens or tightens the nut or bolt.

**Base Unit**: NewtonMeter

**Units**:

- Newton Millimeter (`NewtonMillimeter`). Units: N·mm
- Newton Centimeter (`NewtonCentimeter`). Units: N·cm
- Newton Meter (`NewtonMeter`). Units: N·m
- Poundal Foot (`PoundalFoot`). Units: pdl·ft
- Pound Force Inch (`PoundForceInch`). Units: lbf·in
- Pound Force Foot (`PoundForceFoot`). Units: lbf·ft
- Gram Force Millimeter (`GramForceMillimeter`). Units: gf·mm
- Gram Force Centimeter (`GramForceCentimeter`). Units: gf·cm
- Gram Force Meter (`GramForceMeter`). Units: gf·m
- Kilogram Force Millimeter (`KilogramForceMillimeter`). Units: kgf·mm
- Kilogram Force Centimeter (`KilogramForceCentimeter`). Units: kgf·cm
- Kilogram Force Meter (`KilogramForceMeter`). Units: kgf·m
- Tonne Force Millimeter (`TonneForceMillimeter`). Units: tf·mm
- Tonne Force Centimeter (`TonneForceCentimeter`). Units: tf·cm
- Tonne Force Meter (`TonneForceMeter`). Units: tf·m

### Volume Flow
`VolumeFlow`

In physics and engineering, in particular fluid dynamics and hydrometry, the volumetric flow rate, (also known as volume flow rate, rate of fluid flow or volume velocity) is the volume of fluid which passes through a given surface per unit time. The SI unit is m³/s (cubic meters per second). In US Customary Units and British Imperial Units, volumetric flow rate is often expressed as ft³/s (cubic feet per second). It is usually represented by the symbol Q.

**Base Unit**: CubicMeterPerSecond

**Units**:

- Cubic Meter Per Second (`CubicMeterPerSecond`). Units: m³/s
- Cubic Meter Per Minute (`CubicMeterPerMinute`). Units: m³/min
- Cubic Meter Per Hour (`CubicMeterPerHour`). Units: m³/h
- Cubic Meter Per Day (`CubicMeterPerDay`). Units: m³/d
- Cubic Foot Per Second (`CubicFootPerSecond`). Units: ft³/s
- Cubic Foot Per Minute (`CubicFootPerMinute`). Units: ft³/min, CFM
- Cubic Foot Per Hour (`CubicFootPerHour`). Units: ft³/h, cf/hr
- Cubic Yard Per Second (`CubicYardPerSecond`). Units: yd³/s
- Cubic Yard Per Minute (`CubicYardPerMinute`). Units: yd³/min
- Cubic Yard Per Hour (`CubicYardPerHour`). Units: yd³/h
- Cubic Yard Per Day (`CubicYardPerDay`). Units: cy/day
- Million Us Gallon Per Day (`MillionUsGallonPerDay`). Units: MGD
- Us Gallon Per Day (`UsGallonPerDay`). Units: gpd, gal/d
- Liter Per Second (`LiterPerSecond`). Units: l/s, LPS
- Liter Per Minute (`LiterPerMinute`). Units: l/min, LPM
- Liter Per Hour (`LiterPerHour`). Units: l/h, LPH
- Liter Per Day (`LiterPerDay`). Units: l/day, l/d, LPD
- Us Gallon Per Second (`UsGallonPerSecond`). Units: gal (U.S.)/s
- Us Gallon Per Minute (`UsGallonPerMinute`). Units: gal (U.S.)/min, GPM
- Uk Gallon Per Day (`UkGallonPerDay`). Units: gal (U. K.)/d
- Uk Gallon Per Hour (`UkGallonPerHour`). Units: gal (imp.)/h
- Uk Gallon Per Minute (`UkGallonPerMinute`). Units: gal (imp.)/min
- Uk Gallon Per Second (`UkGallonPerSecond`). Units: gal (imp.)/s
- Kilous Gallon Per Minute (`KilousGallonPerMinute`). Units: kgal (U.S.)/min, KGPM
- Us Gallon Per Hour (`UsGallonPerHour`). Units: gal (U.S.)/h
- Cubic Decimeter Per Minute (`CubicDecimeterPerMinute`). Units: dm³/min
- Oil Barrel Per Day (`OilBarrelPerDay`). Units: bbl/d, BOPD
- Oil Barrel Per Minute (`OilBarrelPerMinute`). Units: bbl/min, bpm
- Oil Barrel Per Hour (`OilBarrelPerHour`). Units: bbl/hr, bph
- Oil Barrel Per Second (`OilBarrelPerSecond`). Units: bbl/s
- Cubic Millimeter Per Second (`CubicMillimeterPerSecond`). Units: mm³/s
- Acre Foot Per Second (`AcreFootPerSecond`). Units: af/s
- Acre Foot Per Minute (`AcreFootPerMinute`). Units: af/m
- Acre Foot Per Hour (`AcreFootPerHour`). Units: af/h
- Acre Foot Per Day (`AcreFootPerDay`). Units: af/d
- Cubic Centimeter Per Minute (`CubicCentimeterPerMinute`). Units: cm³/min

### Volume Flow Per Area
`VolumeFlowPerArea`

**Base Unit**: CubicMeterPerSecondPerSquareMeter

**Units**:

- Cubic Meter Per Second Per Square Meter (`CubicMeterPerSecondPerSquareMeter`). Units: m³/(s·m²)
- Cubic Foot Per Minute Per Square Foot (`CubicFootPerMinutePerSquareFoot`). Units: CFM/ft²

### Volume Per Length
`VolumePerLength`

Volume, typically of fluid, that a container can hold within a unit of length.

**Base Unit**: CubicMeterPerMeter

**Units**:

- Cubic Meter Per Meter (`CubicMeterPerMeter`). Units: m³/m
- Liter Per Meter (`LiterPerMeter`). Units: l/m
- Liter Per Kilometer (`LiterPerKilometer`). Units: l/km
- Liter Per Millimeter (`LiterPerMillimeter`). Units: l/mm
- Oil Barrel Per Foot (`OilBarrelPerFoot`). Units: bbl/ft
- Cubic Yard Per Foot (`CubicYardPerFoot`). Units: yd³/ft
- Cubic Yard Per Us Survey Foot (`CubicYardPerUsSurveyFoot`). Units: yd³/ftUS
- Us Gallon Per Mile (`UsGallonPerMile`). Units: gal (U.S.)/mi
- Imperial Gallon Per Mile (`ImperialGallonPerMile`). Units: gal (imp.)/mi

### Warping Moment Of Inertia
`WarpingMomentOfInertia`

A geometric property of an area that is used to determine the warping stress.

**Base Unit**: MeterToTheSixth

**Units**:

- Meter To The Sixth (`MeterToTheSixth`). Units: m⁶
- Decimeter To The Sixth (`DecimeterToTheSixth`). Units: dm⁶
- Centimeter To The Sixth (`CentimeterToTheSixth`). Units: cm⁶
- Millimeter To The Sixth (`MillimeterToTheSixth`). Units: mm⁶
- Foot To The Sixth (`FootToTheSixth`). Units: ft⁶
- Inch To The Sixth (`InchToTheSixth`). Units: in⁶

## Electrical & Magnetic

### Electric Admittance
`ElectricAdmittance`

Electric admittance is a measure of how easily a circuit or device will allow a current to flow by the combined effect of conductance and susceptance in a circuit. It is defined as the inverse of impedance. The SI unit of admittance is the siemens (symbol S).

**Base Unit**: Siemens

**Units**:

- Siemens (`Siemens`). Units: S
- Mho (`Mho`). Units: ℧

### Electric Apparent Energy
`ElectricApparentEnergy`

A unit for expressing the integral of apparent power over time, equal to the product of 1 volt-ampere and 1 hour, or to 3600 joules.

**Base Unit**: VoltampereHour

**Units**:

- Voltampere Hour (`VoltampereHour`). Units: VAh

### Electric Apparent Power
`ElectricApparentPower`

Power engineers measure apparent power as the magnitude of the vector sum of active and reactive power. It is the product of the root mean square voltage (in volts) and the root mean square current (in amperes).

**Base Unit**: Voltampere

**Units**:

- Voltampere (`Voltampere`). Units: VA

### Electric Capacitance
`ElectricCapacitance`

Capacitance is the capacity of a material object or device to store electric charge.

**Base Unit**: Farad

**Units**:

- Farad (`Farad`). Units: F

### Electric Charge
`ElectricCharge`

Electric charge is the physical property of matter that causes it to experience a force when placed in an electromagnetic field.

**Base Unit**: Coulomb

**Units**:

- Coulomb (`Coulomb`). Units: C
- Ampere Hour (`AmpereHour`). Units: A-h, Ah

### Electric Charge Density
`ElectricChargeDensity`

In electromagnetism, charge density is a measure of the amount of electric charge per volume.

**Base Unit**: CoulombPerCubicMeter

**Units**:

- Coulomb Per Cubic Meter (`CoulombPerCubicMeter`). Units: C/m³

### Electric Conductance
`ElectricConductance`

The electrical conductance of an object is a measure of the ease with which an electric current passes. Along with susceptance, it is one of two elements of admittance. Its reciprocal quantity is electrical resistance.

**Base Unit**: Siemens

**Units**:

- Siemens (`Siemens`). Units: S
- Mho (`Mho`). Units: ℧

### Electric Conductivity
`ElectricConductivity`

Electrical conductivity or specific conductance is the reciprocal of electrical resistivity, and measures a material's ability to conduct an electric current.

**Base Unit**: SiemensPerMeter

**Units**:

- Siemens Per Meter (`SiemensPerMeter`). Units: S/m
- Siemens Per Inch (`SiemensPerInch`). Units: S/in
- Siemens Per Foot (`SiemensPerFoot`). Units: S/ft
- Siemens Per Centimeter (`SiemensPerCentimeter`). Units: S/cm

### Electric Current
`ElectricCurrent`

An electric current is a flow of electric charge. In electric circuits this charge is often carried by moving electrons in a wire. It can also be carried by ions in an electrolyte, or by both ions and electrons such as in a plasma.

**Base Unit**: Ampere

**Units**:

- Ampere (`Ampere`). Units: A

### Electric Current Density
`ElectricCurrentDensity`

In electromagnetism, current density is the electric current per unit area of cross section.

**Base Unit**: AmperePerSquareMeter

**Units**:

- Ampere Per Square Meter (`AmperePerSquareMeter`). Units: A/m²
- Ampere Per Square Inch (`AmperePerSquareInch`). Units: A/in²
- Ampere Per Square Foot (`AmperePerSquareFoot`). Units: A/ft²

### Electric Current Gradient
`ElectricCurrentGradient`

In electromagnetism, the current gradient describes how the current changes in time.

**Base Unit**: AmperePerSecond

**Units**:

- Ampere Per Second (`AmperePerSecond`). Units: A/s
- Ampere Per Minute (`AmperePerMinute`). Units: A/min
- Ampere Per Millisecond (`AmperePerMillisecond`). Units: A/ms
- Ampere Per Microsecond (`AmperePerMicrosecond`). Units: A/μs
- Ampere Per Nanosecond (`AmperePerNanosecond`). Units: A/ns

### Electric Field
`ElectricField`

An electric field is a force field that surrounds electric charges that attracts or repels other electric charges.

**Base Unit**: VoltPerMeter

**Units**:

- Volt Per Meter (`VoltPerMeter`). Units: V/m

### Electric Impedance
`ElectricImpedance`

Electric impedance is the opposition to alternating current presented by the combined effect of resistance and reactance in a circuit. It is defined as the inverse of admittance. The SI unit of impedance is the ohm (symbol Ω).

**Base Unit**: Ohm

**Units**:

- Ohm (`Ohm`). Units: Ω

### Electric Inductance
`ElectricInductance`

Inductance is a property of an electrical conductor which opposes a change in current.

**Base Unit**: Henry

**Units**:

- Henry (`Henry`). Units: H

### Electric Potential
`ElectricPotential`

In classical electromagnetism, the electric potential (a scalar quantity denoted by Φ, ΦE or V and also called the electric field potential or the electrostatic potential) at a point is the amount of electric potential energy that a unitary point charge would have when located at that point.

**Base Unit**: Volt

**Units**:

- Volt (`Volt`). Units: V

### Electric Potential Change Rate
`ElectricPotentialChangeRate`

ElectricPotential change rate is the ratio of the electric potential change to the time during which the change occurred (value of electric potential changes per unit time).

**Base Unit**: VoltPerSecond

**Units**:

- Volt Per Second (`VoltPerSecond`). Units: V/s
- Volt Per Microsecond (`VoltPerMicrosecond`). Units: V/μs
- Volt Per Minute (`VoltPerMinute`). Units: V/min
- Volt Per Hour (`VoltPerHour`). Units: V/h

### Electric Reactance
`ElectricReactance`

In electrical circuits, reactance is the opposition presented to alternating current by inductance and capacitance. Along with resistance, it is one of two elements of impedance.

**Base Unit**: Ohm

**Units**:

- Ohm (`Ohm`). Units: Ω

### Electric Reactive Energy
`ElectricReactiveEnergy`

The volt-ampere reactive hour (expressed as varh) is the reactive power of one Volt-ampere reactive produced in one hour.

**Base Unit**: VoltampereReactiveHour

**Units**:

- Voltampere Reactive Hour (`VoltampereReactiveHour`). Units: varh

### Electric Reactive Power
`ElectricReactivePower`

In electric power transmission and distribution, volt-ampere reactive (var) is a unit of measurement of reactive power. Reactive power exists in an AC circuit when the current and voltage are not in phase.

**Base Unit**: VoltampereReactive

**Units**:

- Voltampere Reactive (`VoltampereReactive`). Units: var

### Electric Resistance
`ElectricResistance`

The electrical resistance of an object is a measure of its opposition to the flow of electric current. Along with reactance, it is one of two elements of impedance. Its reciprocal quantity is electrical conductance.

**Base Unit**: Ohm

**Units**:

- Ohm (`Ohm`). Units: Ω

### Electric Resistivity
`ElectricResistivity`

Electrical resistivity (also known as resistivity, specific electrical resistance, or volume resistivity) is a fundamental property that quantifies how strongly a given material opposes the flow of electric current.

**Base Unit**: OhmMeter

**Units**:

- Ohm Meter (`OhmMeter`). Units: Ω·m
- Ohm Centimeter (`OhmCentimeter`). Units: Ω·cm

### Electric Surface Charge Density
`ElectricSurfaceChargeDensity`

In electromagnetism, surface charge density is a measure of the amount of electric charge per surface area.

**Base Unit**: CoulombPerSquareMeter

**Units**:

- Coulomb Per Square Meter (`CoulombPerSquareMeter`). Units: C/m²
- Coulomb Per Square Centimeter (`CoulombPerSquareCentimeter`). Units: C/cm²
- Coulomb Per Square Inch (`CoulombPerSquareInch`). Units: C/in²

### Electric Susceptance
`ElectricSusceptance`

Electrical susceptance is the imaginary part of admittance, where the real part is conductance.

**Base Unit**: Siemens

**Units**:

- Siemens (`Siemens`). Units: S
- Mho (`Mho`). Units: ℧

### Magnetic Field
`MagneticField`

A magnetic field is a force field that is created by moving electric charges (electric currents) and magnetic dipoles, and exerts a force on other nearby moving charges and magnetic dipoles.

**Base Unit**: Tesla

**Units**:

- Tesla (`Tesla`). Units: T
- Gauss (`Gauss`). Units: G

### Magnetic Flux
`MagneticFlux`

In physics, specifically electromagnetism, the magnetic flux through a surface is the surface integral of the normal component of the magnetic field B passing through that surface.

**Base Unit**: Weber

**Units**:

- Weber (`Weber`). Units: Wb

### Magnetization
`Magnetization`

In classical electromagnetism, magnetization is the vector field that expresses the density of permanent or induced magnetic dipole moments in a magnetic material.

**Base Unit**: AmperePerMeter

**Units**:

- Ampere Per Meter (`AmperePerMeter`). Units: A/m

### Permeability
`Permeability`

In electromagnetism, permeability is the measure of the ability of a material to support the formation of a magnetic field within itself.

**Base Unit**: HenryPerMeter

**Units**:

- Henry Per Meter (`HenryPerMeter`). Units: H/m

### Permittivity
`Permittivity`

In electromagnetism, permittivity is the measure of resistance that is encountered when forming an electric field in a particular medium.

**Base Unit**: FaradPerMeter

**Units**:

- Farad Per Meter (`FaradPerMeter`). Units: F/m

## Thermal

### Coefficient Of Thermal Expansion
`CoefficientOfThermalExpansion`

A unit that represents a fractional change in size in response to a change in temperature.

**Base Unit**: PerKelvin

**Units**:

- Per Kelvin (`PerKelvin`). Units: K⁻¹
- Per Degree Celsius (`PerDegreeCelsius`). Units: °C⁻¹
- Per Degree Fahrenheit (`PerDegreeFahrenheit`). Units: °F⁻¹
- Ppm Per Kelvin (`PpmPerKelvin`). Units: ppm/K
- Ppm Per Degree Celsius (`PpmPerDegreeCelsius`). Units: ppm/°C
- Ppm Per Degree Fahrenheit (`PpmPerDegreeFahrenheit`). Units: ppm/°F

### Energy
`Energy`

The joule, symbol J, is a derived unit of energy, work, or amount of heat in the International System of Units. It is equal to the energy transferred (or work done) when applying a force of one newton through a distance of one metre (1 newton metre or N·m), or in passing an electric current of one ampere through a resistance of one ohm for one second. Many other units of energy are included. Please do not confuse this definition of the calorie with the one colloquially used by the food industry, the large calorie, which is equivalent to 1 kcal. Thermochemical definition of the calorie is used. For BTU, the IT definition is used.

**Base Unit**: Joule

**Units**:

- Joule (`Joule`). Units: J
- Calorie (`Calorie`). Units: cal
- British Thermal Unit (`BritishThermalUnit`). Units: BTU
- Electron Volt (`ElectronVolt`). Units: eV: In physics, an electronvolt (symbol eV, also written electron-volt and electron volt) is the measure of an amount of kinetic energy gained by a single electron accelerating from rest through an electric potential difference of one volt in vacuum. When used as a unit of energy, the numerical value of 1 eV in joules (symbol J) is equivalent to the numerical value of the charge of an electron in coulombs (symbol C). Under the 2019 redefinition of the SI base units, this sets 1 eV equal to the exact value 1.602176634×10−19 J.
- Foot Pound (`FootPound`). Units: ft·lb: A pound-foot (lb⋅ft), abbreviated from pound-force foot (lbf · ft), is a unit of torque representing one pound of force acting at a perpendicular distance of one foot from a pivot point. Conversely one foot pound-force (ft · lbf) is the moment about an axis that applies one pound-force at a radius of one foot.
- Erg (`Erg`). Units: erg: The erg is a unit of energy equal to 10−7 joules (100 nJ). It originated in the Centimetre–gram–second system of units (CGS). It has the symbol erg. The erg is not an SI unit. Its name is derived from ergon (ἔργον), a Greek word meaning 'work' or 'task'.
- Watt Hour (`WattHour`). Units: Wh
- Watt Day (`WattDay`). Units: Wd
- Therm Ec (`ThermEc`). Units: th (E.C.): The therm (symbol, thm) is a non-SI unit of heat energy equal to 100,000 British thermal units (BTU), and approximately 105 megajoules, 29.3 kilowatt-hours, 25,200 kilocalories and 25.2 thermies. One therm is the energy content of approximately 100 cubic feet (2.83 cubic metres) of natural gas at standard temperature and pressure. However, the BTU is not standardised worldwide, with slightly different values in the EU, UK, and United States, meaning that the energy content of the therm also varies by territory.
- Therm Us (`ThermUs`). Units: th (U.S.): The therm (symbol, thm) is a non-SI unit of heat energy equal to 100,000 British thermal units (BTU), and approximately 105 megajoules, 29.3 kilowatt-hours, 25,200 kilocalories and 25.2 thermies. One therm is the energy content of approximately 100 cubic feet (2.83 cubic metres) of natural gas at standard temperature and pressure. However, the BTU is not standardised worldwide, with slightly different values in the EU, UK, and United States, meaning that the energy content of the therm also varies by territory.
- Therm Imperial (`ThermImperial`). Units: th (imp.): The therm (symbol, thm) is a non-SI unit of heat energy equal to 100,000 British thermal units (BTU), and approximately 105 megajoules, 29.3 kilowatt-hours, 25,200 kilocalories and 25.2 thermies. One therm is the energy content of approximately 100 cubic feet (2.83 cubic metres) of natural gas at standard temperature and pressure. However, the BTU is not standardised worldwide, with slightly different values in the EU, UK, and United States, meaning that the energy content of the therm also varies by territory.
- Horsepower Hour (`HorsepowerHour`). Units: hp·h: A horsepower-hour (symbol: hp⋅h) is an outdated unit of energy, not used in the International System of Units. The unit represents an amount of work a horse is supposed capable of delivering during an hour (1 horsepower integrated over a time interval of an hour).

### Energy Density
`EnergyDensity`

**Base Unit**: JoulePerCubicMeter

**Units**:

- Joule Per Cubic Meter (`JoulePerCubicMeter`). Units: J/m³
- Watt Hour Per Cubic Meter (`WattHourPerCubicMeter`). Units: Wh/m³

### Entropy
`Entropy`

Entropy is an important concept in the branch of science known as thermodynamics. The idea of "irreversibility" is central to the understanding of entropy.  It is often said that entropy is an expression of the disorder, or randomness of a system, or of our lack of information about it. Entropy is an extensive property. It has the dimension of energy divided by temperature, which has a unit of joules per kelvin (J/K) in the International System of Units

**Base Unit**: JoulePerKelvin

**Units**:

- Joule Per Kelvin (`JoulePerKelvin`). Units: J/K
- Calorie Per Kelvin (`CaloriePerKelvin`). Units: cal/K
- Joule Per Degree Celsius (`JoulePerDegreeCelsius`). Units: J/°C

### Heat Flux
`HeatFlux`

Heat flux is the flow of energy per unit of area per unit of time

**Base Unit**: WattPerSquareMeter

**Units**:

- Watt Per Square Meter (`WattPerSquareMeter`). Units: W/m²
- Watt Per Square Inch (`WattPerSquareInch`). Units: W/in²
- Watt Per Square Foot (`WattPerSquareFoot`). Units: W/ft²
- Btu Per Second Square Inch (`BtuPerSecondSquareInch`). Units: BTU/(s·in²)
- Btu Per Second Square Foot (`BtuPerSecondSquareFoot`). Units: BTU/(s·ft²)
- Btu Per Minute Square Foot (`BtuPerMinuteSquareFoot`). Units: BTU/(min·ft²)
- Btu Per Hour Square Foot (`BtuPerHourSquareFoot`). Units: BTU/(h·ft²)
- Calorie Per Second Square Centimeter (`CaloriePerSecondSquareCentimeter`). Units: cal/(s·cm²)
- Kilocalorie Per Hour Square Meter (`KilocaloriePerHourSquareMeter`). Units: kcal/(h·m²)
- Pound Force Per Foot Second (`PoundForcePerFootSecond`). Units: lbf/(ft·s)
- Pound Per Second Cubed (`PoundPerSecondCubed`). Units: lb/s³, lbm/s³

### Heat Transfer Coefficient
`HeatTransferCoefficient`

The heat transfer coefficient or film coefficient, or film effectiveness, in thermodynamics and in mechanics is the proportionality constant between the heat flux and the thermodynamic driving force for the flow of heat (i.e., the temperature difference, ΔT)

**Base Unit**: WattPerSquareMeterKelvin

**Units**:

- Watt Per Square Meter Kelvin (`WattPerSquareMeterKelvin`). Units: W/(m²·K)
- Watt Per Square Meter Celsius (`WattPerSquareMeterCelsius`). Units: W/(m²·°C)
- Btu Per Hour Square Foot Degree Fahrenheit (`BtuPerHourSquareFootDegreeFahrenheit`). Units: Btu/(h·ft²·°F), Btu/(ft²·h·°F), Btu/(hr·ft²·°F), Btu/(ft²·hr·°F)
- Calorie Per Hour Square Meter Degree Celsius (`CaloriePerHourSquareMeterDegreeCelsius`). Units: kcal/(h·m²·°C), kcal/(m²·h·°C), kcal/(hr·m²·°C), kcal/(m²·hr·°C)

### Molar Energy
`MolarEnergy`

Molar energy is the amount of energy stored in 1 mole of a substance.

**Base Unit**: JoulePerMole

**Units**:

- Joule Per Mole (`JoulePerMole`). Units: J/mol

### Molar Entropy
`MolarEntropy`

Molar entropy is amount of energy required to increase temperature of 1 mole substance by 1 Kelvin.

**Base Unit**: JoulePerMoleKelvin

**Units**:

- Joule Per Mole Kelvin (`JoulePerMoleKelvin`). Units: J/(mol·K)

### Specific Energy
`SpecificEnergy`

The SpecificEnergy

**Base Unit**: JoulePerKilogram

**Units**:

- Joule Per Kilogram (`JoulePerKilogram`). Units: J/kg
- Mega Joule Per Tonne (`MegaJoulePerTonne`). Units: MJ/t
- Calorie Per Gram (`CaloriePerGram`). Units: cal/g
- Watt Hour Per Kilogram (`WattHourPerKilogram`). Units: Wh/kg
- Watt Day Per Kilogram (`WattDayPerKilogram`). Units: Wd/kg
- Watt Day Per Tonne (`WattDayPerTonne`). Units: Wd/t
- Watt Day Per Short Ton (`WattDayPerShortTon`). Units: Wd/ST
- Watt Hour Per Pound (`WattHourPerPound`). Units: Wh/lbs
- Btu Per Pound (`BtuPerPound`). Units: btu/lb

### Specific Entropy
`SpecificEntropy`

Specific entropy is an amount of energy required to raise temperature of a substance by 1 Kelvin per unit mass.

**Base Unit**: JoulePerKilogramKelvin

**Units**:

- Joule Per Kilogram Kelvin (`JoulePerKilogramKelvin`). Units: J/kg·K
- Joule Per Kilogram Degree Celsius (`JoulePerKilogramDegreeCelsius`). Units: J/kg·°C
- Calorie Per Gram Kelvin (`CaloriePerGramKelvin`). Units: cal/g·K
- Btu Per Pound Fahrenheit (`BtuPerPoundFahrenheit`). Units: BTU/(lb·°F), BTU/(lbm·°F)

### Temperature
`Temperature`

A temperature is a numerical measure of hot or cold. Its measurement is by detection of heat radiation or particle velocity or kinetic energy, or by the bulk behavior of a thermometric material. It may be calibrated in any of various temperature scales, Celsius, Fahrenheit, Kelvin, etc. The fundamental physical definition of temperature is provided by thermodynamics.

**Base Unit**: Kelvin

**Units**:

- Kelvin (`Kelvin`). Units: K
- Degree Celsius (`DegreeCelsius`). Units: °C
- Millidegree Celsius (`MillidegreeCelsius`). Units: m°C
- Degree Delisle (`DegreeDelisle`). Units: °De
- Degree Fahrenheit (`DegreeFahrenheit`). Units: °F
- Degree Newton (`DegreeNewton`). Units: °N
- Degree Rankine (`DegreeRankine`). Units: °R
- Degree Reaumur (`DegreeReaumur`). Units: °Ré
- Degree Roemer (`DegreeRoemer`). Units: °Rø
- Solar Temperature (`SolarTemperature`). Units: T⊙

### Temperature Change Rate
`TemperatureChangeRate`

Temperature change rate is the ratio of the temperature change to the time during which the change occurred (value of temperature changes per unit time).

**Base Unit**: DegreeCelsiusPerSecond

**Units**:

- Degree Celsius Per Second (`DegreeCelsiusPerSecond`). Units: °C/s
- Degree Celsius Per Minute (`DegreeCelsiusPerMinute`). Units: °C/min
- Degree Kelvin Per Minute (`DegreeKelvinPerMinute`). Units: K/min
- Degree Fahrenheit Per Minute (`DegreeFahrenheitPerMinute`). Units: °F/min
- Degree Fahrenheit Per Second (`DegreeFahrenheitPerSecond`). Units: °F/s
- Degree Kelvin Per Second (`DegreeKelvinPerSecond`). Units: K/s
- Degree Celsius Per Hour (`DegreeCelsiusPerHour`). Units: °C/h
- Degree Kelvin Per Hour (`DegreeKelvinPerHour`). Units: K/h
- Degree Fahrenheit Per Hour (`DegreeFahrenheitPerHour`). Units: °F/h

### Temperature Delta
`TemperatureDelta`

Difference between two temperatures. The conversions are different than for Temperature.

**Base Unit**: Kelvin

**Units**:

- Kelvin (`Kelvin`). Units: ∆K
- Degree Celsius (`DegreeCelsius`). Units: ∆°C
- Degree Delisle (`DegreeDelisle`). Units: ∆°De
- Degree Fahrenheit (`DegreeFahrenheit`). Units: ∆°F
- Degree Newton (`DegreeNewton`). Units: ∆°N
- Degree Rankine (`DegreeRankine`). Units: ∆°R
- Degree Reaumur (`DegreeReaumur`). Units: ∆°Ré
- Degree Roemer (`DegreeRoemer`). Units: ∆°Rø

### Temperature Gradient
`TemperatureGradient`

**Base Unit**: KelvinPerMeter

**Units**:

- Kelvin Per Meter (`KelvinPerMeter`). Units: ∆°K/m
- Degree Celsius Per Meter (`DegreeCelsiusPerMeter`). Units: ∆°C/m
- Degree Fahrenheit Per Foot (`DegreeFahrenheitPerFoot`). Units: ∆°F/ft
- Degree Celsius Per Kilometer (`DegreeCelsiusPerKilometer`). Units: ∆°C/km

### Thermal Conductivity
`ThermalConductivity`

Thermal conductivity is the property of a material to conduct heat.

**Base Unit**: WattPerMeterKelvin

**Units**:

- Watt Per Meter Kelvin (`WattPerMeterKelvin`). Units: W/(m·K)
- Btu Per Hour Foot Fahrenheit (`BtuPerHourFootFahrenheit`). Units: BTU/(h·ft·°F)

### Volumetric Heat Capacity
`VolumetricHeatCapacity`

The volumetric heat capacity is the amount of energy that must be added, in the form of heat, to one unit of volume of the material in order to cause an increase of one unit in its temperature.

**Base Unit**: JoulePerCubicMeterKelvin

**Units**:

- Joule Per Cubic Meter Kelvin (`JoulePerCubicMeterKelvin`). Units: J/(m³·K)
- Joule Per Cubic Meter Degree Celsius (`JoulePerCubicMeterDegreeCelsius`). Units: J/(m³·°C)
- Calorie Per Cubic Centimeter Degree Celsius (`CaloriePerCubicCentimeterDegreeCelsius`). Units: cal/(cm³·°C)
- Btu Per Cubic Foot Degree Fahrenheit (`BtuPerCubicFootDegreeFahrenheit`). Units: BTU/(ft³·°F)

## Light & Radiation

### Absorbed Dose Of Ionizing Radiation
`AbsorbedDoseOfIonizingRadiation`

Absorbed dose is a dose quantity which is the measure of the energy deposited in matter by ionizing radiation per unit mass.

**Base Unit**: Gray

**Units**:

- Gray (`Gray`). Units: Gy: The gray is the unit of ionizing radiation dose in the SI, defined as the absorption of one joule of radiation energy per kilogram of matter.
- Rad (`Rad`). Units: rad: The rad is a unit of absorbed radiation dose, defined as 1 rad = 0.01 Gy = 0.01 J/kg.

### Dose Area Product
`DoseAreaProduct`

It is defined as the absorbed dose multiplied by the area irradiated.

**Base Unit**: GraySquareMeter

**Units**:

- Gray Square Meter (`GraySquareMeter`). Units: Gy·m²
- Gray Square Decimeter (`GraySquareDecimeter`). Units: Gy·dm²
- Gray Square Centimeter (`GraySquareCentimeter`). Units: Gy·cm²
- Gray Square Millimeter (`GraySquareMillimeter`). Units: Gy·mm²

### Illuminance
`Illuminance`

In photometry, illuminance is the total luminous flux incident on a surface, per unit area.

**Base Unit**: Lux

**Units**:

- Lux (`Lux`). Units: lx

### Irradiance
`Irradiance`

Irradiance is the intensity of ultraviolet (UV) or visible light incident on a surface.

**Base Unit**: WattPerSquareMeter

**Units**:

- Watt Per Square Meter (`WattPerSquareMeter`). Units: W/m²
- Watt Per Square Centimeter (`WattPerSquareCentimeter`). Units: W/cm²

### Irradiation
`Irradiation`

Irradiation is the process by which an object is exposed to radiation. The exposure can originate from various sources, including natural sources.

**Base Unit**: JoulePerSquareMeter

**Units**:

- Joule Per Square Meter (`JoulePerSquareMeter`). Units: J/m²
- Joule Per Square Centimeter (`JoulePerSquareCentimeter`). Units: J/cm²
- Joule Per Square Millimeter (`JoulePerSquareMillimeter`). Units: J/mm²
- Watt Hour Per Square Meter (`WattHourPerSquareMeter`). Units: Wh/m²
- Btu Per Square Foot (`BtuPerSquareFoot`). Units: Btu/ft²

### Luminance
`Luminance`

**Base Unit**: CandelaPerSquareMeter

**Units**:

- Candela Per Square Meter (`CandelaPerSquareMeter`). Units: Cd/m²
- Candela Per Square Foot (`CandelaPerSquareFoot`). Units: Cd/ft²
- Candela Per Square Inch (`CandelaPerSquareInch`). Units: Cd/in²
- Nit (`Nit`). Units: nt

### Luminosity
`Luminosity`

Luminosity is an absolute measure of radiated electromagnetic power (light), the radiant power emitted by a light-emitting object.

**Base Unit**: Watt

**Units**:

- Watt (`Watt`). Units: W
- Solar Luminosity (`SolarLuminosity`). Units: L⊙: The IAU has defined a nominal solar luminosity of 3.828×10^26 W to promote publication of consistent and comparable values in units of the solar luminosity.

### Luminous Flux
`LuminousFlux`

In photometry, luminous flux or luminous power is the measure of the perceived power of light.

**Base Unit**: Lumen

**Units**:

- Lumen (`Lumen`). Units: lm

### Luminous Intensity
`LuminousIntensity`

In photometry, luminous intensity is a measure of the wavelength-weighted power emitted by a light source in a particular direction per unit solid angle, based on the luminosity function, a standardized model of the sensitivity of the human eye.

**Base Unit**: Candela

**Units**:

- Candela (`Candela`). Units: cd

### Radiation Equivalent Dose
`RadiationEquivalentDose`

Equivalent dose is a dose quantity representing the stochastic health effects of low levels of ionizing radiation on the human body which represents the probability of radiation-induced cancer and genetic damage.

**Base Unit**: Sievert

**Units**:

- Sievert (`Sievert`). Units: Sv: The sievert is a unit in the International System of Units (SI) intended to represent the stochastic health risk of ionizing radiation, which is defined as the probability of causing radiation-induced cancer and genetic damage.
- Roentgen Equivalent Man (`RoentgenEquivalentMan`). Units: rem

### Radiation Equivalent Dose Rate
`RadiationEquivalentDoseRate`

A dose rate is quantity of radiation absorbed or delivered per unit time.

**Base Unit**: SievertPerSecond

**Units**:

- Sievert Per Hour (`SievertPerHour`). Units: Sv/h
- Sievert Per Second (`SievertPerSecond`). Units: Sv/s
- Roentgen Equivalent Man Per Hour (`RoentgenEquivalentManPerHour`). Units: rem/h

### Radiation Exposure
`RadiationExposure`

Radiation exposure is a measure of the ionization of air due to ionizing radiation from photons.

**Base Unit**: CoulombPerKilogram

**Units**:

- Coulomb Per Kilogram (`CoulombPerKilogram`). Units: C/kg
- Roentgen (`Roentgen`). Units: R

### Radioactivity
`Radioactivity`

Amount of ionizing radiation released when an element spontaneously emits energy as a result of the radioactive decay of an unstable atom per unit time.

**Base Unit**: Becquerel

**Units**:

- Becquerel (`Becquerel`). Units: Bq: Activity of a quantity of radioactive material in which one nucleus decays per second.
- Curie (`Curie`). Units: Ci
- Rutherford (`Rutherford`). Units: Rd: Activity of a quantity of radioactive material in which one million nuclei decay per second.

### Vitamin A
`VitaminA`

Vitamin A: 1 IU is the biological equivalent of 0.3 µg retinol, or of 0.6 µg beta-carotene.

**Base Unit**: InternationalUnit

**Units**:

- International Unit (`InternationalUnit`). Units: IU

## Chemical & Material

### Mass Concentration
`MassConcentration`

In chemistry, the mass concentration ρi (or γi) is defined as the mass of a constituent mi divided by the volume of the mixture V

**Base Unit**: KilogramPerCubicMeter

**Units**:

- Gram Per Cubic Millimeter (`GramPerCubicMillimeter`). Units: g/mm³
- Gram Per Cubic Centimeter (`GramPerCubicCentimeter`). Units: g/cm³
- Gram Per Cubic Meter (`GramPerCubicMeter`). Units: g/m³
- Gram Per Microliter (`GramPerMicroliter`). Units: g/μl
- Gram Per Milliliter (`GramPerMilliliter`). Units: g/ml
- Gram Per Deciliter (`GramPerDeciliter`). Units: g/dl
- Gram Per Liter (`GramPerLiter`). Units: g/l
- Tonne Per Cubic Millimeter (`TonnePerCubicMillimeter`). Units: t/mm³
- Tonne Per Cubic Centimeter (`TonnePerCubicCentimeter`). Units: t/cm³
- Tonne Per Cubic Meter (`TonnePerCubicMeter`). Units: t/m³
- Pound Per Cubic Inch (`PoundPerCubicInch`). Units: lb/in³
- Pound Per Cubic Foot (`PoundPerCubicFoot`). Units: lb/ft³
- Slug Per Cubic Foot (`SlugPerCubicFoot`). Units: slug/ft³
- Pound Per U S Gallon (`PoundPerUSGallon`). Units: ppg (U.S.)
- Ounce Per U S Gallon (`OuncePerUSGallon`). Units: oz/gal (U.S.)
- Ounce Per Imperial Gallon (`OuncePerImperialGallon`). Units: oz/gal (imp.)
- Pound Per Imperial Gallon (`PoundPerImperialGallon`). Units: ppg (imp.)

### Molality
`Molality`

Molality is a measure of the amount of solute in a solution relative to a given mass of solvent.

**Base Unit**: MolePerKilogram

**Units**:

- Mole Per Kilogram (`MolePerKilogram`). Units: mol/kg
- Mole Per Gram (`MolePerGram`). Units: mol/g

### Molarity
`Molarity`

Molar concentration, also called molarity, amount concentration or substance concentration, is a measure of the concentration of a solute in a solution, or of any chemical species, in terms of amount of substance in a given volume.

**Base Unit**: MolePerCubicMeter

**Units**:

- Mole Per Cubic Meter (`MolePerCubicMeter`). Units: mol/m³
- Mole Per Liter (`MolePerLiter`). Units: mol/l, M
- Pound Mole Per Cubic Foot (`PoundMolePerCubicFoot`). Units: lbmol/ft³

### Porous Medium Permeability
`PorousMediumPermeability`

**Base Unit**: SquareMeter

**Units**:

- Darcy (`Darcy`). Units: D: The darcy (or darcy unit) and millidarcy (md or mD) are units of permeability, named after Henry Darcy. They are not SI units, but they are widely used in petroleum engineering and geology.
- Square Meter (`SquareMeter`). Units: m²
- Square Centimeter (`SquareCentimeter`). Units: cm²

### Turbidity
`Turbidity`

Turbidity is the cloudiness or haziness of a fluid caused by large numbers of individual particles that are generally invisible to the naked eye, similar to smoke in air. The measurement of turbidity is a key test of water quality.

**Base Unit**: NTU

**Units**:

- NTU (`NTU`). Units: NTU

### Volume Concentration
`VolumeConcentration`

The volume concentration (not to be confused with volume fraction) is defined as the volume of a constituent divided by the total volume of the mixture.

**Base Unit**: DecimalFraction

**Units**:

- Decimal Fraction (`DecimalFraction`)
- Liter Per Liter (`LiterPerLiter`). Units: l/l
- Liter Per Milliliter (`LiterPerMilliliter`). Units: l/ml
- Percent (`Percent`). Units: %, % (v/v)
- Part Per Thousand (`PartPerThousand`). Units: ‰
- Part Per Million (`PartPerMillion`). Units: ppm
- Part Per Billion (`PartPerBillion`). Units: ppb
- Part Per Trillion (`PartPerTrillion`). Units: ppt

## Ratios & Levels

### Amplitude Ratio
`AmplitudeRatio`

The strength of a signal expressed in decibels (dB) relative to one volt RMS.

**Base Unit**: DecibelVolt

**Units**:

- Decibel Volt (`DecibelVolt`). Units: dBV
- Decibel Microvolt (`DecibelMicrovolt`). Units: dBµV
- Decibel Millivolt (`DecibelMillivolt`). Units: dBmV
- Decibel Unloaded (`DecibelUnloaded`). Units: dBu

### Level
`Level`

Level is the logarithm of the ratio of a quantity Q to a reference value of that quantity, Q₀, expressed in dimensionless units.

**Base Unit**: Decibel

**Units**:

- Decibel (`Decibel`). Units: dB
- Neper (`Neper`). Units: Np

### Ratio
`Ratio`

In mathematics, a ratio is a relationship between two numbers of the same kind (e.g., objects, persons, students, spoonfuls, units of whatever identical dimension), usually expressed as "a to b" or a:b, sometimes expressed arithmetically as a dimensionless quotient of the two that explicitly indicates how many times the first number contains the second (not necessarily an integer).

**Base Unit**: DecimalFraction

**Units**:

- Decimal Fraction (`DecimalFraction`)
- Percent (`Percent`). Units: %
- Part Per Thousand (`PartPerThousand`). Units: ‰
- Part Per Million (`PartPerMillion`). Units: ppm
- Part Per Billion (`PartPerBillion`). Units: ppb
- Part Per Trillion (`PartPerTrillion`). Units: ppt

### Ratio Change Rate
`RatioChangeRate`

The change in ratio per unit of time.

**Base Unit**: DecimalFractionPerSecond

**Units**:

- Percent Per Second (`PercentPerSecond`). Units: %/s
- Decimal Fraction Per Second (`DecimalFractionPerSecond`). Units: /s

### Relative Humidity
`RelativeHumidity`

Relative humidity is a ratio of the actual water vapor present in the air to the maximum water vapor in the air at the given temperature.

**Base Unit**: Percent

**Units**:

- Percent (`Percent`). Units: %RH

## Other

### Area Density
`AreaDensity`

The area density of a two-dimensional object is calculated as the mass per unit area. For paper this is also called grammage.

**Base Unit**: KilogramPerSquareMeter

**Units**:

- Kilogram Per Square Meter (`KilogramPerSquareMeter`). Units: kg/m²
- Gram Per Square Meter (`GramPerSquareMeter`). Units: g/m², gsm: Also known as grammage for paper industry. In fiber industry used with abbreviation 'gsm'.
- Milligram Per Square Meter (`MilligramPerSquareMeter`). Units: mg/m²

### Area Moment Of Inertia
`AreaMomentOfInertia`

A geometric property of an area that reflects how its points are distributed with regard to an axis.

**Base Unit**: MeterToTheFourth

**Units**:

- Meter To The Fourth (`MeterToTheFourth`). Units: m⁴
- Decimeter To The Fourth (`DecimeterToTheFourth`). Units: dm⁴
- Centimeter To The Fourth (`CentimeterToTheFourth`). Units: cm⁴
- Millimeter To The Fourth (`MillimeterToTheFourth`). Units: mm⁴
- Foot To The Fourth (`FootToTheFourth`). Units: ft⁴
- Inch To The Fourth (`InchToTheFourth`). Units: in⁴

### Bit Rate
`BitRate`

In telecommunications and computing, bit rate is the number of bits that are conveyed or processed per unit of time.

**Base Unit**: BitPerSecond

**Units**:

- Bit Per Second (`BitPerSecond`). Units: bit/s, bps
- Byte Per Second (`BytePerSecond`). Units: B/s
- Octet Per Second (`OctetPerSecond`). Units: o/s

### Fluid Resistance
`FluidResistance`

Fluid Resistance is a force acting opposite to the relative motion of any object moving with respect to a surrounding fluid. Fluid Resistance is sometimes referred to as drag or fluid friction.

**Base Unit**: PascalSecondPerCubicMeter

**Units**:

- Pascal Second Per Liter (`PascalSecondPerLiter`). Units: Pa·s/l
- Pascal Minute Per Liter (`PascalMinutePerLiter`). Units: Pa·min/l
- Pascal Second Per Milliliter (`PascalSecondPerMilliliter`). Units: Pa·s/ml
- Pascal Minute Per Milliliter (`PascalMinutePerMilliliter`). Units: Pa·min/ml
- Pascal Second Per Cubic Meter (`PascalSecondPerCubicMeter`). Units: Pa·s/m³
- Pascal Minute Per Cubic Meter (`PascalMinutePerCubicMeter`). Units: Pa·min/m³
- Pascal Second Per Cubic Centimeter (`PascalSecondPerCubicCentimeter`). Units: Pa·s/cm³
- Pascal Minute Per Cubic Centimeter (`PascalMinutePerCubicCentimeter`). Units: Pa·min/cm³
- Dyne Second Per Centimeter To The Fifth (`DyneSecondPerCentimeterToTheFifth`). Units: dyn·s/cm⁵, dyn·s·cm⁻⁵
- Millimeter Mercury Second Per Liter (`MillimeterMercurySecondPerLiter`). Units: mmHg·s/l
- Millimeter Mercury Minute Per Liter (`MillimeterMercuryMinutePerLiter`). Units: mmHg·min/l
- Millimeter Mercury Second Per Milliliter (`MillimeterMercurySecondPerMilliliter`). Units: mmHg·s/ml
- Millimeter Mercury Minute Per Milliliter (`MillimeterMercuryMinutePerMilliliter`). Units: mmHg·min/ml
- Millimeter Mercury Second Per Cubic Centimeter (`MillimeterMercurySecondPerCubicCentimeter`). Units: mmHg·s/cm³
- Millimeter Mercury Minute Per Cubic Centimeter (`MillimeterMercuryMinutePerCubicCentimeter`). Units: mmHg·min/cm³
- Millimeter Mercury Second Per Cubic Meter (`MillimeterMercurySecondPerCubicMeter`). Units: mmHg·s/m³
- Millimeter Mercury Minute Per Cubic Meter (`MillimeterMercuryMinutePerCubicMeter`). Units: mmHg·min/m³
- Wood Unit (`WoodUnit`). Units: WU, HRU

### Frequency
`Frequency`

The number of occurrences of a repeating event per unit time.

**Base Unit**: Hertz

**Units**:

- Hertz (`Hertz`). Units: Hz
- Radian Per Second (`RadianPerSecond`). Units: rad/s: In SI units, angular frequency is normally presented with the unit radian per second, and need not express a rotational value. The unit hertz (Hz) is dimensionally equivalent, but by convention it is only used for frequency f, never for angular frequency ω. This convention is used to help avoid the confusion that arises when dealing with quantities such as frequency and angular quantities because the units of measure (such as cycle or radian) are considered to be one and hence may be omitted when expressing quantities in terms of SI units.
- Cycle Per Minute (`CyclePerMinute`). Units: cpm
- Cycle Per Hour (`CyclePerHour`). Units: cph
- Beat Per Minute (`BeatPerMinute`). Units: bpm
- Per Second (`PerSecond`). Units: s⁻¹

### Leak Rate
`LeakRate`

A leakage rate of QL = 1 Pa-m³/s is given when the pressure in a closed, evacuated container with a volume of 1 m³ rises by 1 Pa per second or when the pressure in the container drops by 1 Pa in the event of overpressure.

**Base Unit**: PascalCubicMeterPerSecond

**Units**:

- Pascal Cubic Meter Per Second (`PascalCubicMeterPerSecond`). Units: Pa·m³/s
- Millibar Liter Per Second (`MillibarLiterPerSecond`). Units: mbar·l/s
- Torr Liter Per Second (`TorrLiterPerSecond`). Units: Torr·l/s

### Linear Power Density
`LinearPowerDensity`

The Linear Power Density of a substance is its power per unit length.  The term linear density is most often used when describing the characteristics of one-dimensional objects, although linear density can also be used to describe the density of a three-dimensional quantity along one particular dimension.

**Base Unit**: WattPerMeter

**Units**:

- Watt Per Meter (`WattPerMeter`). Units: W/m
- Watt Per Centimeter (`WattPerCentimeter`). Units: W/cm
- Watt Per Millimeter (`WattPerMillimeter`). Units: W/mm
- Watt Per Inch (`WattPerInch`). Units: W/in
- Watt Per Foot (`WattPerFoot`). Units: W/ft

### Molar Flow
`MolarFlow`

Molar flow is the ratio of the amount of substance change to the time during which the change occurred (value of amount of substance changes per unit time).

**Base Unit**: MolePerSecond

**Units**:

- Mole Per Second (`MolePerSecond`). Units: mol/s
- Mole Per Minute (`MolePerMinute`). Units: mol/min
- Mole Per Hour (`MolePerHour`). Units: kmol/h
- Pound Mole Per Second (`PoundMolePerSecond`). Units: lbmol/s
- Pound Mole Per Minute (`PoundMolePerMinute`). Units: lbmol/min
- Pound Mole Per Hour (`PoundMolePerHour`). Units: lbmol/h

### Molar Mass
`MolarMass`

In chemistry, the molar mass M is a physical property defined as the mass of a given substance (chemical element or chemical compound) divided by the amount of substance.

**Base Unit**: KilogramPerMole

**Units**:

- Gram Per Mole (`GramPerMole`). Units: g/mol
- Kilogram Per Kilomole (`KilogramPerKilomole`). Units: kg/kmol
- Pound Per Mole (`PoundPerMole`). Units: lb/mol

### Power
`Power`

In physics, power is the rate of doing work. It is equivalent to an amount of energy consumed per unit time.

**Base Unit**: Watt

**Units**:

- Watt (`Watt`). Units: W
- Mechanical Horsepower (`MechanicalHorsepower`). Units: hp(I): Assuming the third CGPM (1901, CR 70) definition of standard gravity, gn = 9.80665 m/s2, is used to define the pound-force as well as the kilogram force, and the international avoirdupois pound (1959), one imperial horsepower is: 76.0402249 × 9.80665 kg⋅m2/s3
- Metric Horsepower (`MetricHorsepower`). Units: hp(M): DIN 66036 defines one metric horsepower as the power to raise a mass of 75 kilograms against the Earth's gravitational force over a distance of one metre in one second:[18] 75 kg × 9.80665 m/s2 × 1 m / 1 s = 75 kgf⋅m/s = 1 PS. This is equivalent to 735.49875 W, or 98.6% of an imperial horsepower.
- Electrical Horsepower (`ElectricalHorsepower`). Units: hp(E): Nameplates on electrical motors show their power output, not the power input (the power delivered at the shaft, not the power consumed to drive the motor). This power output is ordinarily stated in watts or kilowatts. In the United States, the power output is stated in horsepower, which for this purpose is defined as exactly 746 W.
- Boiler Horsepower (`BoilerHorsepower`). Units: hp(S): Boiler horsepower is a boiler's capacity to deliver steam to a steam engine and is not the same unit of power as the 550 ft lb/s definition. One boiler horsepower is equal to the thermal energy rate required to evaporate 34.5 pounds (15.6 kg) of fresh water at 212 °F (100 °C) in one hour.
- Hydraulic Horsepower (`HydraulicHorsepower`). Units: hp(H): Hydraulic horsepower can represent the power available within hydraulic machinery, power through the down-hole nozzle of a drilling rig, or can be used to estimate the mechanical power needed to generate a known hydraulic flow rate.
- British Thermal Unit Per Hour (`BritishThermalUnitPerHour`). Units: Btu/h, Btu/hr
- Joule Per Hour (`JoulePerHour`). Units: J/h
- Ton Of Refrigeration (`TonOfRefrigeration`). Units: TR

### Power Density
`PowerDensity`

The amount of power in a volume.

**Base Unit**: WattPerCubicMeter

**Units**:

- Watt Per Cubic Meter (`WattPerCubicMeter`). Units: W/m³
- Watt Per Cubic Inch (`WattPerCubicInch`). Units: W/in³
- Watt Per Cubic Foot (`WattPerCubicFoot`). Units: W/ft³
- Watt Per Liter (`WattPerLiter`). Units: W/l

### Power Ratio
`PowerRatio`

The strength of a signal expressed in decibels (dB) relative to one watt.

**Base Unit**: DecibelWatt

**Units**:

- Decibel Watt (`DecibelWatt`). Units: dBW
- Decibel Milliwatt (`DecibelMilliwatt`). Units: dBmW, dBm

### Reciprocal Area
`ReciprocalArea`

Reciprocal area (Inverse-square) quantity is used to specify a physical quantity inversely proportional to the square of the distance.

**Base Unit**: InverseSquareMeter

**Units**:

- Inverse Square Meter (`InverseSquareMeter`). Units: m⁻²
- Inverse Square Kilometer (`InverseSquareKilometer`). Units: km⁻²
- Inverse Square Decimeter (`InverseSquareDecimeter`). Units: dm⁻²
- Inverse Square Centimeter (`InverseSquareCentimeter`). Units: cm⁻²
- Inverse Square Millimeter (`InverseSquareMillimeter`). Units: mm⁻²
- Inverse Square Micrometer (`InverseSquareMicrometer`). Units: µm⁻²
- Inverse Square Mile (`InverseSquareMile`). Units: mi⁻²
- Inverse Square Yard (`InverseSquareYard`). Units: yd⁻²
- Inverse Square Foot (`InverseSquareFoot`). Units: ft⁻²
- Inverse Us Survey Square Foot (`InverseUsSurveySquareFoot`). Units: ft⁻² (US)
- Inverse Square Inch (`InverseSquareInch`). Units: in⁻²

### Reciprocal Length
`ReciprocalLength`

Reciprocal (Inverse) Length is used in various fields of science and mathematics. It is defined as the inverse value of a length unit.

**Base Unit**: InverseMeter

**Units**:

- Inverse Meter (`InverseMeter`). Units: m⁻¹, 1/m
- Inverse Centimeter (`InverseCentimeter`). Units: cm⁻¹, 1/cm
- Inverse Millimeter (`InverseMillimeter`). Units: mm⁻¹, 1/mm
- Inverse Mile (`InverseMile`). Units: mi⁻¹, 1/mi
- Inverse Yard (`InverseYard`). Units: yd⁻¹, 1/yd
- Inverse Foot (`InverseFoot`). Units: ft⁻¹, 1/ft
- Inverse Us Survey Foot (`InverseUsSurveyFoot`). Units: ftUS⁻¹, 1/ftUS
- Inverse Inch (`InverseInch`). Units: in⁻¹, 1/in
- Inverse Mil (`InverseMil`). Units: mil⁻¹, 1/mil
- Inverse Microinch (`InverseMicroinch`). Units: µin⁻¹, 1/µin

### Thermal Insulance
`ThermalInsulance`

Thermal insulance (R-value) is a measure of a material's resistance to the heat current. It quantifies how effectively a material can resist the transfer of heat through conduction, convection, and radiation. It has the units square metre kelvins per watt (m2⋅K/W) in SI units or square foot degree Fahrenheit–hours per British thermal unit (ft2⋅°F⋅h/Btu) in imperial units. The higher the thermal insulance, the better a material insulates against heat transfer. It is commonly used in construction to assess the insulation properties of materials such as walls, roofs, and insulation products.

**Base Unit**: SquareMeterKelvinPerKilowatt

**Units**:

- Square Meter Kelvin Per Kilowatt (`SquareMeterKelvinPerKilowatt`). Units: m²K/kW
- Square Meter Kelvin Per Watt (`SquareMeterKelvinPerWatt`). Units: m²K/W
- Square Meter Degree Celsius Per Watt (`SquareMeterDegreeCelsiusPerWatt`). Units: m²°C/W
- Square Centimeter Kelvin Per Watt (`SquareCentimeterKelvinPerWatt`). Units: cm²K/W
- Square Millimeter Kelvin Per Watt (`SquareMillimeterKelvinPerWatt`). Units: mm²K/W
- Square Centimeter Hour Degree Celsius Per Kilocalorie (`SquareCentimeterHourDegreeCelsiusPerKilocalorie`). Units: cm²Hr°C/kcal
- Hour Square Feet Degree Fahrenheit Per Btu (`HourSquareFeetDegreeFahrenheitPerBtu`). Units: Hrft²°F/Btu

