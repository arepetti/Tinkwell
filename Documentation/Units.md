# Supported Units

This document lists all the quantity types and their corresponding units of measurement. It is generated from UnitsNet package JSON data.

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
  - [Vitamin A](#vitamin-a)

## Basic Dimensions

### Amount Of Substance
`AmountOfSubstance`

Mole is the amount of substance containing Avagadro's Number (6.02 x 10 ^ 23) of real particles such as molecules,atoms, ions or radicals.

**Default Unit**: Mole

**Units**:

- Mole (`Mole`).

  Abbreviation(s): mol
- Pound Mole (`PoundMole`).

  Abbreviation(s): lbmol

### Angle
`Angle`

In geometry, an angle is the figure formed by two rays, called the sides of the angle, sharing a common endpoint, called the vertex of the angle.

**Default Unit**: Radian

**Units**:

- Radian (`Radian`).

  Abbreviation(s): rad
- Degree (`Degree`).

  Abbreviation(s): °, deg
- Arcminute (`Arcminute`).

  Abbreviation(s): ', arcmin, amin, min
- Arcsecond (`Arcsecond`).

  Abbreviation(s): ″, arcsec, asec, sec
- Gradian (`Gradian`).

  Abbreviation(s): g
- Nato Mil (`NatoMil`).

  Abbreviation(s): mil
- Revolution (`Revolution`).

  Abbreviation(s): r

### Area
`Area`

Area is a quantity that expresses the extent of a two-dimensional surface or shape, or planar lamina, in the plane. Area can be understood as the amount of material with a given thickness that would be necessary to fashion a model of the shape, or the amount of paint necessary to cover the surface with a single coat.[1] It is the two-dimensional analog of the length of a curve (a one-dimensional concept) or the volume of a solid (a three-dimensional concept).

**Default Unit**: SquareMeter

**Units**:

- Square Kilometer (`SquareKilometer`).

  Abbreviation(s): km²
- Square Meter (`SquareMeter`).

  Abbreviation(s): m²
- Square Decimeter (`SquareDecimeter`).

  Abbreviation(s): dm²
- Square Centimeter (`SquareCentimeter`).

  Abbreviation(s): cm²
- Square Millimeter (`SquareMillimeter`).

  Abbreviation(s): mm²
- Square Micrometer (`SquareMicrometer`).

  Abbreviation(s): µm²
- Square Mile (`SquareMile`).

  Abbreviation(s): mi²

  The statute mile was standardised between the British Commonwealth and the United States by an international agreement in 1959, when it was formally redefined with respect to SI units as exactly 1,609.344 metres.
- Square Yard (`SquareYard`).

  Abbreviation(s): yd²

  The yard (symbol: yd) is an English unit of length in both the British imperial and US customary systems of measurement equalling 3 feet (or 36 inches). Since 1959 the yard has been by international agreement standardized as exactly 0.9144 meter. A distance of 1,760 yards is equal to 1 mile.
- Square Foot (`SquareFoot`).

  Abbreviation(s): ft²
- Us Survey Square Foot (`UsSurveySquareFoot`).

  Abbreviation(s): ft² (US)

  In the United States, the foot was defined as 12 inches, with the inch being defined by the Mendenhall Order of 1893 as 39.37 inches = 1 m. This makes a U.S. survey foot exactly 1200/3937 meters.
- Square Inch (`SquareInch`).

  Abbreviation(s): in²
- Acre (`Acre`).

  Abbreviation(s): ac

  Based upon the international yard and pound agreement of 1959, an acre may be declared as exactly 4,046.8564224 square metres.
- Hectare (`Hectare`).

  Abbreviation(s): ha
- Square Nautical Mile (`SquareNauticalMile`).

  Abbreviation(s): nmi²

### Duration
`Duration`

Time is a dimension in which events can be ordered from the past through the present into the future, and also the measure of durations of events and the intervals between them.

**Default Unit**: Second

**Units**:

- Year365 (`Year365`).

  Abbreviation(s): yr, year, years
- Month30 (`Month30`).

  Abbreviation(s): mo, month, months
- Week (`Week`).

  Abbreviation(s): wk, week, weeks
- Day (`Day`).

  Abbreviation(s): d, day, days
- Hour (`Hour`).

  Abbreviation(s): h, hr, hrs, hour, hours
- Minute (`Minute`).

  Abbreviation(s): m, min, minute, minutes
- Second (`Second`).

  Abbreviation(s): s, sec, secs, second, seconds
- Julian Year (`JulianYear`).

  Abbreviation(s): jyr, jyear, jyears
- Sol (`Sol`).

  Abbreviation(s): sol

### Information
`Information`

In computing and telecommunications, a unit of information is the capacity of some standard data storage system or communication channel, used to measure the capacities of other systems and channels. In information theory, units of information are also used to measure the information contents or entropy of random variables.

**Default Unit**: Bit

**Units**:

- Byte (`Byte`).

  Abbreviation(s): B
- Octet (`Octet`).

  Abbreviation(s): o
- Bit (`Bit`).

  Abbreviation(s): b

### Length
`Length`

Many different units of length have been used around the world. The main units in modern use are U.S. customary units in the United States and the Metric system elsewhere. British Imperial units are still used for some purposes in the United Kingdom and some other countries. The metric system is sub-divided into SI and non-SI units.

**Default Unit**: Meter

**Units**:

- Meter (`Meter`).

  Abbreviation(s): m
- Mile (`Mile`).

  Abbreviation(s): mi

  The statute mile was standardised between the British Commonwealth and the United States by an international agreement in 1959, when it was formally redefined with respect to SI units as exactly 1,609.344 metres.
- Yard (`Yard`).

  Abbreviation(s): yd

  The yard (symbol: yd) is an English unit of length in both the British imperial and US customary systems of measurement equalling 3 feet (or 36 inches). Since 1959 the yard has been by international agreement standardized as exactly 0.9144 meter. A distance of 1,760 yards is equal to 1 mile.
- Foot (`Foot`).

  Abbreviation(s): ft, ', ′

  The foot (pl. feet; standard symbol: ft) is a unit of length in the British imperial and United States customary systems of measurement. The prime symbol, ′, is commonly used to represent the foot. In both customary and imperial units, one foot comprises 12 inches, and one yard comprises three feet. Since an international agreement in 1959, the foot is defined as equal to exactly 0.3048 meters.
- Us Survey Foot (`UsSurveyFoot`).

  Abbreviation(s): ftUS

  In the United States, the foot was defined as 12 inches, with the inch being defined by the Mendenhall Order of 1893 as 39.37 inches = 1 m. This makes a U.S. survey foot exactly 1200/3937 meters.
- Inch (`Inch`).

  Abbreviation(s): in, \", ″

  The inch (symbol: in or ″) is a unit of length in the British Imperial and the United States customary systems of measurement. It is equal to 1/36 yard or 1/12 of a foot. Derived from the Roman uncia ("twelfth"), the word inch is also sometimes used to translate similar units in other measurement systems, usually understood as deriving from the width of the human thumb.
- Mil (`Mil`).

  Abbreviation(s): mil
- Nautical Mile (`NauticalMile`).

  Abbreviation(s): NM, nmi
- Fathom (`Fathom`).

  Abbreviation(s): fathom
- Shackle (`Shackle`).

  Abbreviation(s): shackle
- Microinch (`Microinch`).

  Abbreviation(s): µin
- Printer Point (`PrinterPoint`).

  Abbreviation(s): pt

  In typography, the point is the smallest unit of measure. It is used for measuring font size, leading, and other items on a printed page. In modern times this size of the point has been approximated as exactly 1⁄72.27 (0.01383700013837) of the inch by Donald Knuth for the default unit of his TeX computer typesetting system and is thus sometimes known as the TeX point.
- Dtp Point (`DtpPoint`).

  Abbreviation(s): pt

  The desktop publishing point (DTP) is defined as 1⁄72 of an inch (1/72 × 25.4 mm ≈ 0.353 mm) and, as with earlier American point sizes, is considered to be 1⁄12 of a pica.
- Printer Pica (`PrinterPica`).

  Abbreviation(s): pica

  The American pica of 0.16604 inches (~4.217 mm) was established by the United States Type Founders' Association in 1886. In TeX one pica is 400⁄2409 of an inch.
- Dtp Pica (`DtpPica`).

  Abbreviation(s): pica

  The pica is a typographic unit of measure corresponding to approximately 1⁄6 of an inch, or from 1⁄68 to 1⁄73 of a foot. One pica is further divided into 12 points.
- Twip (`Twip`).

  Abbreviation(s): twip

  A twip (abbreviating "twentieth of a point" or "twentieth of an inch point") is a typographical measurement, defined as 1⁄20 of a typographical point. One twip is 1⁄1440 inch, or ~17.64 μm.
- Hand (`Hand`).

  Abbreviation(s): h, hh

  The hand is a non-SI unit of measurement of length standardized to 4 in (101.6 mm). It is used to measure the height of horses in many English-speaking countries, including Australia, Canada, Ireland, the United Kingdom, and the United States. It was originally based on the breadth of a human hand.
- Astronomical Unit (`AstronomicalUnit`).

  Abbreviation(s): au, ua

  One Astronomical Unit is the distance from the solar system Star, the sun, to planet Earth.
- Parsec (`Parsec`).

  Abbreviation(s): pc

  A parsec is defined as the distance at which one astronomical unit (AU) subtends an angle of one arcsecond.
- Light Year (`LightYear`).

  Abbreviation(s): ly

  A Light Year (ly) is the distance that light travel during an Earth year, ie 365 days.
- Solar Radius (`SolarRadius`).

  Abbreviation(s): R⊙

  Solar radius is a ratio unit to the radius of the solar system star, the sun.
- Chain (`Chain`).

  Abbreviation(s): ch

  The chain (abbreviated ch) is a unit of length equal to 66 feet (22 yards), used in both the US customary and Imperial unit systems. It is subdivided into 100 links. There are 10 chains in a furlong, and 80 chains in one statute mile. In metric terms, it is 20.1168 m long.
- Angstrom (`Angstrom`).

  Abbreviation(s): Å, A

  Angstrom is a metric unit of length equal to 1e-10 meter
- Data Mile (`DataMile`).

  Abbreviation(s): DM

  In radar-related subjects and in JTIDS, a data mile is a unit of distance equal to 6000 feet (1.8288 kilometres or 0.987 nautical miles).

### Mass
`Mass`

In physics, mass (from Greek μᾶζα "barley cake, lump [of dough]") is a property of a physical system or body, giving rise to the phenomena of the body's resistance to being accelerated by a force and the strength of its mutual gravitational attraction with other bodies. Instruments such as mass balances or scales use those phenomena to measure mass. The SI unit of mass is the kilogram (kg).

**Default Unit**: Kilogram

**Units**:

- Gram (`Gram`).

  Abbreviation(s): g
- Tonne (`Tonne`).

  Abbreviation(s): t

  The tonne is a unit of mass equal to 1,000 kilograms. It is a non-SI unit accepted for use with SI. It is also referred to as a metric ton in the United States to distinguish it from the non-metric units of the short ton (United States customary units) and the long ton (British imperial units). It is equivalent to approximately 2,204.6 pounds, 1.102 short tons, and 0.984 long tons.
- Short Ton (`ShortTon`).

  Abbreviation(s): t (short), short tn, ST

  The short ton is a unit of mass equal to 2,000 pounds (907.18474 kg), that is most commonly used in the United States – known there simply as the ton.
- Long Ton (`LongTon`).

  Abbreviation(s): long tn

  Long ton (weight ton or Imperial ton) is a unit of mass equal to 2,240 pounds (1,016 kg) and is the name for the unit called the "ton" in the avoirdupois or Imperial system of measurements that was used in the United Kingdom and several other Commonwealth countries before metrication.
- Pound (`Pound`).

  Abbreviation(s): lb, lbs, lbm

  The pound or pound-mass (abbreviations: lb, lbm) is a unit of mass used in the imperial, United States customary and other systems of measurement. A number of different definitions have been used, the most common today being the international avoirdupois pound which is legally defined as exactly 0.45359237 kilograms, and which is divided into 16 avoirdupois ounces.
- Ounce (`Ounce`).

  Abbreviation(s): oz

  The international avoirdupois ounce (abbreviated oz) is defined as exactly 28.349523125 g under the international yard and pound agreement of 1959, signed by the United States and countries of the Commonwealth of Nations. 16 oz make up an avoirdupois pound.
- Slug (`Slug`).

  Abbreviation(s): slug

  The slug (abbreviation slug) is a unit of mass that is accelerated by 1 ft/s² when a force of one pound (lbf) is exerted on it.
- Stone (`Stone`).

  Abbreviation(s): st

  The stone (abbreviation st) is a unit of mass equal to 14 pounds avoirdupois (about 6.35 kilograms) used in Great Britain and Ireland for measuring human body weight.
- Short Hundredweight (`ShortHundredweight`).

  Abbreviation(s): cwt

  The short hundredweight (abbreviation cwt) is a unit of mass equal to 100 pounds in US and Canada. In British English, the short hundredweight is referred to as the "cental".
- Long Hundredweight (`LongHundredweight`).

  Abbreviation(s): cwt

  The long or imperial hundredweight (abbreviation cwt) is a unit of mass equal to 112 pounds in US and Canada.
- Grain (`Grain`).

  Abbreviation(s): gr

  A grain is a unit of measurement of mass, and in the troy weight, avoirdupois, and Apothecaries' system, equal to exactly 64.79891 milligrams.
- Solar Mass (`SolarMass`).

  Abbreviation(s): M☉, M⊙

  Solar mass is a ratio unit to the mass of the solar system star, the sun.
- Earth Mass (`EarthMass`).

  Abbreviation(s): em

  Earth mass is a ratio unit to the mass of planet Earth.

### Scalar
`Scalar`

A way of representing a number of items.

**Default Unit**: Amount

**Units**:

- Amount (`Amount`)

### Solid Angle
`SolidAngle`

In geometry, a solid angle is the two-dimensional angle in three-dimensional space that an object subtends at a point.

**Default Unit**: Steradian

**Units**:

- Steradian (`Steradian`).

  Abbreviation(s): sr

### Volume
`Volume`

Volume is the quantity of three-dimensional space enclosed by some closed boundary, for example, the space that a substance (solid, liquid, gas, or plasma) or shape occupies or contains.[1] Volume is often quantified numerically using the SI derived unit, the cubic metre. The volume of a container is generally understood to be the capacity of the container, i. e. the amount of fluid (gas or liquid) that the container could hold, rather than the amount of space the container itself displaces.

**Default Unit**: CubicMeter

**Units**:

- Liter (`Liter`).

  Abbreviation(s): l
- Cubic Meter (`CubicMeter`).

  Abbreviation(s): m³
- Cubic Kilometer (`CubicKilometer`).

  Abbreviation(s): km³
- Cubic Hectometer (`CubicHectometer`).

  Abbreviation(s): hm³
- Cubic Decimeter (`CubicDecimeter`).

  Abbreviation(s): dm³
- Cubic Centimeter (`CubicCentimeter`).

  Abbreviation(s): cm³
- Cubic Millimeter (`CubicMillimeter`).

  Abbreviation(s): mm³
- Cubic Micrometer (`CubicMicrometer`).

  Abbreviation(s): µm³
- Cubic Mile (`CubicMile`).

  Abbreviation(s): mi³

  A cubic mile (abbreviation: cu mi or mi3) is an imperial and US customary (non-SI non-metric) unit of volume, used in the United States, Canada and the United Kingdom. It is defined as the volume of a cube with sides of 1 mile (63360 inches, 5280 feet, 1760 yards or ~1.609 kilometres) in length.
- Cubic Yard (`CubicYard`).

  Abbreviation(s): yd³

  A cubic yard is an Imperial / U.S. customary (non-SI non-metric) unit of volume, used in Canada and the United States. It is defined as the volume of a cube with sides of 1 yard (3 feet, 36 inches, 0.9144 meters) in length.
- Cubic Foot (`CubicFoot`).

  Abbreviation(s): ft³

  The cubic foot (symbol ft3 or cu ft) is an imperial and US customary (non-metric) unit of volume, used in the United States and the United Kingdom. It is defined as the volume of a cube with sides of one foot (0.3048 m) in length.
- Cubic Inch (`CubicInch`).

  Abbreviation(s): in³

  The cubic inch (symbol in3) is a unit of volume in the Imperial units and United States customary units systems. It is the volume of a cube with each of its three dimensions (length, width, and height) being one inch long which is equivalent to 1/231 of a US gallon.
- Imperial Gallon (`ImperialGallon`).

  Abbreviation(s): gal (imp.)

  The British imperial gallon (frequently called simply "gallon") is defined as exactly 4.54609 litres.
- Imperial Ounce (`ImperialOunce`).

  Abbreviation(s): oz (imp.)

  An imperial fluid ounce is 1⁄20 of an imperial pint, 1⁄160 of an imperial gallon or exactly 28.4130625 mL.
- Us Gallon (`UsGallon`).

  Abbreviation(s): gal (U.S.)

  The US liquid gallon (frequently called simply "gallon") is legally defined as 231 cubic inches, which is exactly 3.785411784 litres.
- Us Ounce (`UsOunce`).

  Abbreviation(s): oz (U.S.)

  A US customary fluid ounce is 1⁄16 of a US liquid pint and 1⁄128 of a US liquid gallon or exactly 29.5735295625 mL, making it about 4.08% larger than the imperial fluid ounce.
- Us Tablespoon (`UsTablespoon`).

  Abbreviation(s): tablespoon (U.S.)

  The traditional U.S. interpretation of the tablespoon as a unit of volume is: 1 US tablespoon = 4 fluid drams, or 3 teaspoons or 1/2 US fluid ounce (≈ 14.8 ml)
- Au Tablespoon (`AuTablespoon`).

  Abbreviation(s): tablespoon (A.U.)

  In Australia, the definition of the tablespoon is 20 ml (0.70 imp fl oz).
- Uk Tablespoon (`UkTablespoon`).

  Abbreviation(s): tablespoon (U.K.)

  In nutrition labeling in the U.S. and the U.K., a tablespoon is defined as 15 ml (0.51 US fl oz). In Australia, the definition of the tablespoon is 20 ml (0.70 imp fl oz).
- Metric Teaspoon (`MetricTeaspoon`).

  Abbreviation(s): tsp, t, ts, tspn, t., ts., tsp., tspn., teaspoon

  The metric teaspoon as a unit of culinary measure is 5 ml (0.18 imp fl oz; 0.17 US fl oz),[17] equal to 5 cm3, 1⁄3 UK/Canadian metric tablespoon, or 1⁄4 Australian metric tablespoon.
- Us Teaspoon (`UsTeaspoon`).

  Abbreviation(s): teaspoon (U.S.)

  As a unit of culinary measure, one teaspoon in the United States is 1⁄3 tablespoon, exactly 4.92892159375 ml, 1 1⁄3 US fluid drams, 1⁄6 US fl oz, 1⁄48 US cup, 1⁄768 US liquid gallon, or 77⁄256 (0.30078125) cubic inches.
- Metric Cup (`MetricCup`).

  Abbreviation(s): metric cup

  Australia, Canada, New Zealand, and some other members of the Commonwealth of Nations, being former British colonies that have since metricated, employ a metric cup of 250 millilitres. Although derived from the metric system, it is not an SI unit.
- Us Customary Cup (`UsCustomaryCup`).

  Abbreviation(s): cup (U.S. customary)

  In the United States, the customary cup is half of a liquid pint or 1⁄16 US customary gallon which is 236.5882365 milliliters exactly.
- Us Legal Cup (`UsLegalCup`).

  Abbreviation(s): cup (U.S.)

  The cup currently used in the United States for nutrition labelling is defined in United States law as 240 ml.
- Oil Barrel (`OilBarrel`).

  Abbreviation(s): bbl

  In the oil industry, one barrel (unit symbol bbl) is a unit of volume used for measuring oil defined as exactly 42 US gallons, approximately 159 liters, or 35 imperial gallons.
- Us Beer Barrel (`UsBeerBarrel`).

  Abbreviation(s): bl (U.S.)

  Fluid barrels vary depending on what is being measured and where. In the US most fluid barrels (apart from oil) are 31.5 US gallons (26 imp gal; 119 L) (half a hogshead), but a beer barrel is 31 US gallons (26 imp gal; 117 L).
- Imperial Beer Barrel (`ImperialBeerBarrel`).

  Abbreviation(s): bl (imp.)

  Fluid barrels vary depending on what is being measured and where. In the UK a beer barrel is 36 imperial gallons (43 US gal; ~164 L).
- Us Quart (`UsQuart`).

  Abbreviation(s): qt (U.S.)

  The US liquid quart equals 57.75 cubic inches, which is exactly equal to 0.946352946 L.
- Imperial Quart (`ImperialQuart`).

  Abbreviation(s): qt (imp.)

  The imperial quart, which is used for both liquid and dry capacity, is equal to one quarter of an imperial gallon, or exactly 1.1365225 liters.
- Us Pint (`UsPint`).

  Abbreviation(s): pt (U.S.)

  The pint is a unit of volume or capacity in both the imperial and United States customary measurement systems. In both of those systems it is traditionally one eighth of a gallon. The British imperial pint is about 20% larger than the American pint because the two systems are defined differently.
- Acre Foot (`AcreFoot`).

  Abbreviation(s): ac-ft, acre-foot, acre-feet

  An acre-foot is 43,560 cubic feet (~1,233.5 m3).
- Imperial Pint (`ImperialPint`).

  Abbreviation(s): pt (imp.), UK pt, pt, p

  The pint is a unit of volume or capacity in both the imperial and United States customary measurement systems. In both of those systems it is traditionally one eighth of a gallon. The British imperial pint is about 20% larger than the American pint because the two systems are defined differently.
- Board Foot (`BoardFoot`).

  Abbreviation(s): bf, board foot, board feet

  The board foot or board-foot is a unit of measurement for the volume of lumber in the United States and Canada. It equals the volume of a board that is one-foot (305 mm) in length, one-foot (305 mm) in width, and one-inch (25.4 mm) in thickness.

## Mechanics

### Acceleration
`Acceleration`

Acceleration, in physics, is the rate at which the velocity of an object changes over time. An object's acceleration is the net result of any and all forces acting on the object, as described by Newton's Second Law. The SI unit for acceleration is the Meter per second squared (m/s²). Accelerations are vector quantities (they have magnitude and direction) and add according to the parallelogram law. As a vector, the calculated net force is equal to the product of the object's mass (a scalar quantity) and the acceleration.

**Default Unit**: MeterPerSecondSquared

**Units**:

- Meter Per Second Squared (`MeterPerSecondSquared`).

  Abbreviation(s): m/s²
- Inch Per Second Squared (`InchPerSecondSquared`).

  Abbreviation(s): in/s²
- Foot Per Second Squared (`FootPerSecondSquared`).

  Abbreviation(s): ft/s²
- Knot Per Second (`KnotPerSecond`).

  Abbreviation(s): kn/s
- Knot Per Minute (`KnotPerMinute`).

  Abbreviation(s): kn/min

  The knot (/nɒt/) is a unit of speed equal to one nautical mile per hour, exactly 1.852 km/h (approximately 1.151 mph or 0.514 m/s).
- Knot Per Hour (`KnotPerHour`).

  Abbreviation(s): kn/h

  The knot (/nɒt/) is a unit of speed equal to one nautical mile per hour, exactly 1.852 km/h (approximately 1.151 mph or 0.514 m/s).
- Standard Gravity (`StandardGravity`).

  Abbreviation(s): g

### Brake Specific Fuel Consumption
`BrakeSpecificFuelConsumption`

Brake specific fuel consumption (BSFC) is a measure of the fuel efficiency of any prime mover that burns fuel and produces rotational, or shaft, power. It is typically used for comparing the efficiency of internal combustion engines with a shaft output.

**Default Unit**: KilogramPerJoule

**Units**:

- Gram Per Kilo Watt Hour (`GramPerKiloWattHour`).

  Abbreviation(s): g/kWh
- Kilogram Per Joule (`KilogramPerJoule`).

  Abbreviation(s): kg/J
- Pound Per Mechanical Horsepower Hour (`PoundPerMechanicalHorsepowerHour`).

  Abbreviation(s): lb/hph

  The pound per horse power hour uses mechanical horse power and the imperial pound

### Compressibility
`Compressibility`

**Default Unit**: InversePascal

**Units**:

- Inverse Pascal (`InversePascal`).

  Abbreviation(s): Pa⁻¹, 1/Pa
- Inverse Kilopascal (`InverseKilopascal`).

  Abbreviation(s): kPa⁻¹, 1/kPa
- Inverse Megapascal (`InverseMegapascal`).

  Abbreviation(s): MPa⁻¹, 1/MPa
- Inverse Atmosphere (`InverseAtmosphere`).

  Abbreviation(s): atm⁻¹, 1/atm
- Inverse Millibar (`InverseMillibar`).

  Abbreviation(s): mbar⁻¹, 1/mbar
- Inverse Bar (`InverseBar`).

  Abbreviation(s): bar⁻¹, 1/bar
- Inverse Pound Force Per Square Inch (`InversePoundForcePerSquareInch`).

  Abbreviation(s): psi⁻¹, 1/psi

### Density
`Density`

The density, or more precisely, the volumetric mass density, of a substance is its mass per unit volume.

**Default Unit**: KilogramPerCubicMeter

**Units**:

- Gram Per Cubic Millimeter (`GramPerCubicMillimeter`).

  Abbreviation(s): g/mm³
- Gram Per Cubic Centimeter (`GramPerCubicCentimeter`).

  Abbreviation(s): g/cm³
- Gram Per Cubic Meter (`GramPerCubicMeter`).

  Abbreviation(s): g/m³
- Pound Per Cubic Inch (`PoundPerCubicInch`).

  Abbreviation(s): lb/in³, lbm/in³
- Pound Per Cubic Foot (`PoundPerCubicFoot`).

  Abbreviation(s): lb/ft³, lbm/ft³
- Pound Per Cubic Yard (`PoundPerCubicYard`).

  Abbreviation(s): lb/yd³, lbm/yd³

  Calculated from the definition of <a href="https://en.wikipedia.org/wiki/Pound_(mass)">pound</a> and <a href="https://en.wikipedia.org/wiki/Cubic_yard">Cubic yard</a> compared to metric kilogram and meter.
- Tonne Per Cubic Millimeter (`TonnePerCubicMillimeter`).

  Abbreviation(s): t/mm³
- Tonne Per Cubic Centimeter (`TonnePerCubicCentimeter`).

  Abbreviation(s): t/cm³
- Tonne Per Cubic Meter (`TonnePerCubicMeter`).

  Abbreviation(s): t/m³
- Slug Per Cubic Foot (`SlugPerCubicFoot`).

  Abbreviation(s): slug/ft³
- Gram Per Liter (`GramPerLiter`).

  Abbreviation(s): g/l
- Gram Per Deciliter (`GramPerDeciliter`).

  Abbreviation(s): g/dl
- Gram Per Milliliter (`GramPerMilliliter`).

  Abbreviation(s): g/ml
- Pound Per U S Gallon (`PoundPerUSGallon`).

  Abbreviation(s): ppg (U.S.)
- Pound Per Imperial Gallon (`PoundPerImperialGallon`).

  Abbreviation(s): ppg (imp.)
- Kilogram Per Liter (`KilogramPerLiter`).

  Abbreviation(s): kg/l
- Tonne Per Cubic Foot (`TonnePerCubicFoot`).

  Abbreviation(s): t/ft³
- Tonne Per Cubic Inch (`TonnePerCubicInch`).

  Abbreviation(s): t/in³
- Gram Per Cubic Foot (`GramPerCubicFoot`).

  Abbreviation(s): g/ft³
- Gram Per Cubic Inch (`GramPerCubicInch`).

  Abbreviation(s): g/in³
- Pound Per Cubic Meter (`PoundPerCubicMeter`).

  Abbreviation(s): lb/m³, lbm/m³
- Pound Per Cubic Centimeter (`PoundPerCubicCentimeter`).

  Abbreviation(s): lb/cm³, lbm/cm³
- Pound Per Cubic Millimeter (`PoundPerCubicMillimeter`).

  Abbreviation(s): lb/mm³, lbm/mm³
- Slug Per Cubic Meter (`SlugPerCubicMeter`).

  Abbreviation(s): slug/m³
- Slug Per Cubic Centimeter (`SlugPerCubicCentimeter`).

  Abbreviation(s): slug/cm³
- Slug Per Cubic Millimeter (`SlugPerCubicMillimeter`).

  Abbreviation(s): slug/mm³
- Slug Per Cubic Inch (`SlugPerCubicInch`).

  Abbreviation(s): slug/in³

### Dynamic Viscosity
`DynamicViscosity`

The dynamic (shear) viscosity of a fluid expresses its resistance to shearing flows, where adjacent layers move parallel to each other with different speeds

**Default Unit**: NewtonSecondPerMeterSquared

**Units**:

- Newton Second Per Meter Squared (`NewtonSecondPerMeterSquared`).

  Abbreviation(s): Ns/m²
- Pascal Second (`PascalSecond`).

  Abbreviation(s): Pa·s, PaS
- Poise (`Poise`).

  Abbreviation(s): P
- Reyn (`Reyn`).

  Abbreviation(s): reyn
- Pound Force Second Per Square Inch (`PoundForceSecondPerSquareInch`).

  Abbreviation(s): lbf·s/in²
- Pound Force Second Per Square Foot (`PoundForceSecondPerSquareFoot`).

  Abbreviation(s): lbf·s/ft²
- Pound Per Foot Second (`PoundPerFootSecond`).

  Abbreviation(s): lb/(ft·s)

### Force
`Force`

In physics, a force is any influence that causes an object to undergo a certain change, either concerning its movement, direction, or geometrical construction. In other words, a force can cause an object with mass to change its velocity (which includes to begin moving from a state of rest), i.e., to accelerate, or a flexible object to deform, or both. Force can also be described by intuitive concepts such as a push or a pull. A force has both magnitude and direction, making it a vector quantity. It is measured in the SI unit of newtons and represented by the symbol F.

**Default Unit**: Newton

**Units**:

- Dyn (`Dyn`).

  Abbreviation(s): dyn

  One dyne is equal to 10 micronewtons, 10e−5 N or to 10 nsn (nanosthenes) in the old metre–tonne–second system of units.
- Kilogram Force (`KilogramForce`).

  Abbreviation(s): kgf

  The kilogram-force, or kilopond, is equal to the magnitude of the force exerted on one kilogram of mass in a 9.80665 m/s2 gravitational field (standard gravity). Therefore, one kilogram-force is by definition equal to 9.80665 N.
- Tonne Force (`TonneForce`).

  Abbreviation(s): tf, Ton

  The tonne-force, metric ton-force, megagram-force, and megapond (Mp) are each 1000 kilograms-force.
- Newton (`Newton`).

  Abbreviation(s): N

  The newton (symbol: N) is the unit of force in the International System of Units (SI). It is defined as 1 kg⋅m/s2, the force which gives a mass of 1 kilogram an acceleration of 1 metre per second per second.
- Kilo Pond (`KiloPond`).

  Abbreviation(s): kp

  The kilogram-force, or kilopond, is equal to the magnitude of the force exerted on one kilogram of mass in a 9.80665 m/s2 gravitational field (standard gravity). Therefore, one kilogram-force is by definition equal to 9.80665 N.
- Poundal (`Poundal`).

  Abbreviation(s): pdl

  The poundal is defined as the force necessary to accelerate 1 pound-mass at 1 foot per second per second. 1 pdl = 0.138254954376 N exactly.
- Pound Force (`PoundForce`).

  Abbreviation(s): lbf

  The standard values of acceleration of the standard gravitational field (gn) and the international avoirdupois pound (lb) result in a pound-force equal to 4.4482216152605 N.
- Ounce Force (`OunceForce`).

  Abbreviation(s): ozf

  An ounce-force is 1⁄16 of a pound-force, or about 0.2780139 newtons.
- Short Ton Force (`ShortTonForce`).

  Abbreviation(s): tf (short), t (US)f, short tons-force

  The short ton-force is a unit of force equal to 2,000 pounds-force (907.18474 kgf), that is most commonly used in the United States – known there simply as the ton or US ton.

### Force Change Rate
`ForceChangeRate`

Force change rate is the ratio of the force change to the time during which the change occurred (value of force changes per unit time).

**Default Unit**: NewtonPerSecond

**Units**:

- Newton Per Minute (`NewtonPerMinute`).

  Abbreviation(s): N/min
- Newton Per Second (`NewtonPerSecond`).

  Abbreviation(s): N/s
- Pound Force Per Minute (`PoundForcePerMinute`).

  Abbreviation(s): lbf/min
- Pound Force Per Second (`PoundForcePerSecond`).

  Abbreviation(s): lbf/s

### Force Per Length
`ForcePerLength`

The magnitude of force per unit length.

**Default Unit**: NewtonPerMeter

**Units**:

- Newton Per Meter (`NewtonPerMeter`).

  Abbreviation(s): N/m
- Newton Per Centimeter (`NewtonPerCentimeter`).

  Abbreviation(s): N/cm
- Newton Per Millimeter (`NewtonPerMillimeter`).

  Abbreviation(s): N/mm
- Kilogram Force Per Meter (`KilogramForcePerMeter`).

  Abbreviation(s): kgf/m
- Kilogram Force Per Centimeter (`KilogramForcePerCentimeter`).

  Abbreviation(s): kgf/cm
- Kilogram Force Per Millimeter (`KilogramForcePerMillimeter`).

  Abbreviation(s): kgf/mm
- Tonne Force Per Meter (`TonneForcePerMeter`).

  Abbreviation(s): tf/m
- Tonne Force Per Centimeter (`TonneForcePerCentimeter`).

  Abbreviation(s): tf/cm
- Tonne Force Per Millimeter (`TonneForcePerMillimeter`).

  Abbreviation(s): tf/mm
- Pound Force Per Foot (`PoundForcePerFoot`).

  Abbreviation(s): lbf/ft
- Pound Force Per Inch (`PoundForcePerInch`).

  Abbreviation(s): lbf/in
- Pound Force Per Yard (`PoundForcePerYard`).

  Abbreviation(s): lbf/yd
- Kilopound Force Per Foot (`KilopoundForcePerFoot`).

  Abbreviation(s): kipf/ft, kip/ft, k/ft
- Kilopound Force Per Inch (`KilopoundForcePerInch`).

  Abbreviation(s): kipf/in, kip/in, k/in

### Fuel Efficiency
`FuelEfficiency`

In the context of transport, fuel economy is the energy efficiency of a particular vehicle, given as a ratio of distance traveled per unit of fuel consumed. In most countries, using the metric system, fuel economy is stated as "fuel consumption" in liters per 100 kilometers (L/100 km) or kilometers per liter (km/L or kmpl). In countries using non-metric system, fuel economy is expressed in miles per gallon (mpg) (imperial galon or US galon).

**Default Unit**: KilometerPerLiter

**Units**:

- Liter Per100 Kilometers (`LiterPer100Kilometers`).

  Abbreviation(s): l/100km
- Mile Per Us Gallon (`MilePerUsGallon`).

  Abbreviation(s): mpg (U.S.)
- Mile Per Uk Gallon (`MilePerUkGallon`).

  Abbreviation(s): mpg (imp.)
- Kilometer Per Liter (`KilometerPerLiter`).

  Abbreviation(s): km/l

### Impulse
`Impulse`

In classical mechanics, impulse is the integral of a force, F, over the time interval, t, for which it acts. Impulse applied to an object produces an equivalent vector change in its linear momentum, also in the resultant direction.

**Default Unit**: NewtonSecond

**Units**:

- Kilogram Meter Per Second (`KilogramMeterPerSecond`).

  Abbreviation(s): kg·m/s
- Newton Second (`NewtonSecond`).

  Abbreviation(s): N·s
- Pound Foot Per Second (`PoundFootPerSecond`).

  Abbreviation(s): lb·ft/s
- Pound Force Second (`PoundForceSecond`).

  Abbreviation(s): lbf·s
- Slug Foot Per Second (`SlugFootPerSecond`).

  Abbreviation(s): slug·ft/s

### Jerk
`Jerk`

**Default Unit**: MeterPerSecondCubed

**Units**:

- Meter Per Second Cubed (`MeterPerSecondCubed`).

  Abbreviation(s): m/s³
- Inch Per Second Cubed (`InchPerSecondCubed`).

  Abbreviation(s): in/s³
- Foot Per Second Cubed (`FootPerSecondCubed`).

  Abbreviation(s): ft/s³
- Standard Gravities Per Second (`StandardGravitiesPerSecond`).

  Abbreviation(s): g/s

### Kinematic Viscosity
`KinematicViscosity`

The viscosity of a fluid is a measure of its resistance to gradual deformation by shear stress or tensile stress.

**Default Unit**: SquareMeterPerSecond

**Units**:

- Square Meter Per Second (`SquareMeterPerSecond`).

  Abbreviation(s): m²/s
- Stokes (`Stokes`).

  Abbreviation(s): St
- Square Foot Per Second (`SquareFootPerSecond`).

  Abbreviation(s): ft²/s

### Linear Density
`LinearDensity`

The Linear Density, or more precisely, the linear mass density, of a substance is its mass per unit length.  The term linear density is most often used when describing the characteristics of one-dimensional objects, although linear density can also be used to describe the density of a three-dimensional quantity along one particular dimension.

**Default Unit**: KilogramPerMeter

**Units**:

- Gram Per Millimeter (`GramPerMillimeter`).

  Abbreviation(s): g/mm
- Gram Per Centimeter (`GramPerCentimeter`).

  Abbreviation(s): g/cm
- Gram Per Meter (`GramPerMeter`).

  Abbreviation(s): g/m
- Pound Per Inch (`PoundPerInch`).

  Abbreviation(s): lb/in
- Pound Per Foot (`PoundPerFoot`).

  Abbreviation(s): lb/ft
- Gram Per Foot (`GramPerFoot`).

  Abbreviation(s): g/ft

### Mass Flow
`MassFlow`

Mass flow is the ratio of the mass change to the time during which the change occurred (value of mass changes per unit time).

**Default Unit**: GramPerSecond

**Units**:

- Gram Per Second (`GramPerSecond`).

  Abbreviation(s): g/s, g/S
- Gram Per Day (`GramPerDay`).

  Abbreviation(s): g/d
- Gram Per Hour (`GramPerHour`).

  Abbreviation(s): g/h
- Kilogram Per Hour (`KilogramPerHour`).

  Abbreviation(s): kg/h
- Kilogram Per Minute (`KilogramPerMinute`).

  Abbreviation(s): kg/min
- Tonne Per Hour (`TonnePerHour`).

  Abbreviation(s): t/h
- Pound Per Day (`PoundPerDay`).

  Abbreviation(s): lb/d
- Pound Per Hour (`PoundPerHour`).

  Abbreviation(s): lb/h
- Pound Per Minute (`PoundPerMinute`).

  Abbreviation(s): lb/min
- Pound Per Second (`PoundPerSecond`).

  Abbreviation(s): lb/s
- Tonne Per Day (`TonnePerDay`).

  Abbreviation(s): t/d
- Short Ton Per Hour (`ShortTonPerHour`).

  Abbreviation(s): short tn/h

### Mass Flux
`MassFlux`

Mass flux is the mass flow rate per unit area.

**Default Unit**: KilogramPerSecondPerSquareMeter

**Units**:

- Gram Per Second Per Square Meter (`GramPerSecondPerSquareMeter`).

  Abbreviation(s): g·s⁻¹·m⁻²
- Gram Per Second Per Square Centimeter (`GramPerSecondPerSquareCentimeter`).

  Abbreviation(s): g·s⁻¹·cm⁻²
- Gram Per Second Per Square Millimeter (`GramPerSecondPerSquareMillimeter`).

  Abbreviation(s): g·s⁻¹·mm⁻²
- Gram Per Hour Per Square Meter (`GramPerHourPerSquareMeter`).

  Abbreviation(s): g·h⁻¹·m⁻²
- Gram Per Hour Per Square Centimeter (`GramPerHourPerSquareCentimeter`).

  Abbreviation(s): g·h⁻¹·cm⁻²
- Gram Per Hour Per Square Millimeter (`GramPerHourPerSquareMillimeter`).

  Abbreviation(s): g·h⁻¹·mm⁻²

### Mass Fraction
`MassFraction`

The mass fraction is defined as the mass of a constituent divided by the total mass of the mixture.

**Default Unit**: DecimalFraction

**Units**:

- Decimal Fraction (`DecimalFraction`)
- Gram Per Gram (`GramPerGram`).

  Abbreviation(s): g/g
- Gram Per Kilogram (`GramPerKilogram`).

  Abbreviation(s): g/kg
- Percent (`Percent`).

  Abbreviation(s): %, % (w/w)
- Part Per Thousand (`PartPerThousand`).

  Abbreviation(s): ‰
- Part Per Million (`PartPerMillion`).

  Abbreviation(s): ppm
- Part Per Billion (`PartPerBillion`).

  Abbreviation(s): ppb
- Part Per Trillion (`PartPerTrillion`).

  Abbreviation(s): ppt

### Mass Moment Of Inertia
`MassMomentOfInertia`

A property of body reflects how its mass is distributed with regard to an axis.

**Default Unit**: KilogramSquareMeter

**Units**:

- Gram Square Meter (`GramSquareMeter`).

  Abbreviation(s): g·m²
- Gram Square Decimeter (`GramSquareDecimeter`).

  Abbreviation(s): g·dm²
- Gram Square Centimeter (`GramSquareCentimeter`).

  Abbreviation(s): g·cm²
- Gram Square Millimeter (`GramSquareMillimeter`).

  Abbreviation(s): g·mm²
- Tonne Square Meter (`TonneSquareMeter`).

  Abbreviation(s): t·m²
- Tonne Square Decimeter (`TonneSquareDecimeter`).

  Abbreviation(s): t·dm²
- Tonne Square Centimeter (`TonneSquareCentimeter`).

  Abbreviation(s): t·cm²
- Tonne Square Millimeter (`TonneSquareMillimeter`).

  Abbreviation(s): t·mm²
- Pound Square Foot (`PoundSquareFoot`).

  Abbreviation(s): lb·ft²
- Pound Square Inch (`PoundSquareInch`).

  Abbreviation(s): lb·in²
- Slug Square Foot (`SlugSquareFoot`).

  Abbreviation(s): slug·ft²
- Slug Square Inch (`SlugSquareInch`).

  Abbreviation(s): slug·in²

### Pressure
`Pressure`

Pressure (symbol: P or p) is the ratio of force to the area over which that force is distributed. Pressure is force per unit area applied in a direction perpendicular to the surface of an object. Gauge pressure (also spelled gage pressure)[a] is the pressure relative to the local atmospheric or ambient pressure. Pressure is measured in any unit of force divided by any unit of area. The SI unit of pressure is the newton per square metre, which is called the pascal (Pa) after the seventeenth-century philosopher and scientist Blaise Pascal. A pressure of 1 Pa is small; it approximately equals the pressure exerted by a dollar bill resting flat on a table. Everyday pressures are often stated in kilopascals (1 kPa = 1000 Pa).

**Default Unit**: Pascal

**Units**:

- Pascal (`Pascal`).

  Abbreviation(s): Pa
- Atmosphere (`Atmosphere`).

  Abbreviation(s): atm

  The standard atmosphere (symbol: atm) is a unit of pressure defined as 101325 Pa. It is sometimes used as a reference pressure or standard pressure. It is approximately equal to Earth's average atmospheric pressure at sea level.
- Bar (`Bar`).

  Abbreviation(s): bar

  The bar is a metric unit of pressure defined as 100,000 Pa (100 kPa), though not part of the International System of Units (SI). A pressure of 1 bar is slightly less than the current average atmospheric pressure on Earth at sea level (approximately 1.013 bar).
- Kilogram Force Per Square Meter (`KilogramForcePerSquareMeter`).

  Abbreviation(s): kgf/m²
- Kilogram Force Per Square Centimeter (`KilogramForcePerSquareCentimeter`).

  Abbreviation(s): kgf/cm²

  A kilogram-force per centimetre square (kgf/cm2), often just kilogram per square centimetre (kg/cm2), or kilopond per centimetre square (kp/cm2) is a deprecated unit of pressure using metric units. It is not a part of the International System of Units (SI), the modern metric system. 1 kgf/cm2 equals 98.0665 kPa (kilopascals). It is also known as a technical atmosphere (symbol: at).
- Kilogram Force Per Square Millimeter (`KilogramForcePerSquareMillimeter`).

  Abbreviation(s): kgf/mm²
- Newton Per Square Meter (`NewtonPerSquareMeter`).

  Abbreviation(s): N/m²
- Newton Per Square Centimeter (`NewtonPerSquareCentimeter`).

  Abbreviation(s): N/cm²
- Newton Per Square Millimeter (`NewtonPerSquareMillimeter`).

  Abbreviation(s): N/mm²
- Technical Atmosphere (`TechnicalAtmosphere`).

  Abbreviation(s): at

  A kilogram-force per centimetre square (kgf/cm2), often just kilogram per square centimetre (kg/cm2), or kilopond per centimetre square (kp/cm2) is a deprecated unit of pressure using metric units. It is not a part of the International System of Units (SI), the modern metric system. 1 kgf/cm2 equals 98.0665 kPa (kilopascals). It is also known as a technical atmosphere (symbol: at).
- Torr (`Torr`).

  Abbreviation(s): torr

  The torr (symbol: Torr) is a unit of pressure based on an absolute scale, defined as exactly 1/760 of a standard atmosphere (101325 Pa). Thus one torr is exactly 101325/760 pascals (≈ 133.32 Pa).
- Pound Force Per Square Inch (`PoundForcePerSquareInch`).

  Abbreviation(s): psi, lb/in²
- Pound Force Per Square Mil (`PoundForcePerSquareMil`).

  Abbreviation(s): lb/mil², lbs/mil²
- Pound Force Per Square Foot (`PoundForcePerSquareFoot`).

  Abbreviation(s): lb/ft²
- Tonne Force Per Square Millimeter (`TonneForcePerSquareMillimeter`).

  Abbreviation(s): tf/mm²
- Tonne Force Per Square Meter (`TonneForcePerSquareMeter`).

  Abbreviation(s): tf/m²
- Meter Of Head (`MeterOfHead`).

  Abbreviation(s): m of head
- Tonne Force Per Square Centimeter (`TonneForcePerSquareCentimeter`).

  Abbreviation(s): tf/cm²
- Foot Of Head (`FootOfHead`).

  Abbreviation(s): ft of head
- Millimeter Of Mercury (`MillimeterOfMercury`).

  Abbreviation(s): mmHg

  A millimetre of mercury is a manometric unit of pressure, formerly defined as the extra pressure generated by a column of mercury one millimetre high, and currently defined as exactly 133.322387415 pascals.
- Inch Of Mercury (`InchOfMercury`).

  Abbreviation(s): inHg

  Inch of mercury (inHg and ″Hg) is a non-SI unit of measurement for pressure. It is used for barometric pressure in weather reports, refrigeration and aviation in the United States. It is the pressure exerted by a column of mercury 1 inch (25.4 mm) in height at the standard acceleration of gravity.
- Dyne Per Square Centimeter (`DynePerSquareCentimeter`).

  Abbreviation(s): dyn/cm²
- Pound Per Inch Second Squared (`PoundPerInchSecondSquared`).

  Abbreviation(s): lbm/(in·s²), lb/(in·s²)
- Meter Of Water Column (`MeterOfWaterColumn`).

  Abbreviation(s): mH₂O, mH2O, m wc, m wg

  A centimetre of water is defined as the pressure exerted by a column of water of 1 cm in height at 4 °C (temperature of maximum density) at the standard acceleration of gravity, so that 1 cmH2O (4°C) = 999.9720 kg/m3 × 9.80665 m/s2 × 1 cm = 98.063754138 Pa, but conventionally a nominal maximum water density of 1000 kg/m3 is used, giving 98.0665 Pa.
- Inch Of Water Column (`InchOfWaterColumn`).

  Abbreviation(s): inH2O, inch wc, wc

  Inches of water is a non-SI unit for pressure. It is defined as the pressure exerted by a column of water of 1 inch in height at defined conditions. At a temperature of 4 °C (39.2 °F) pure water has its highest density (1000 kg/m3). At that temperature and assuming the standard acceleration of gravity, 1 inAq is approximately 249.082 pascals (0.0361263 psi).

### Pressure Change Rate
`PressureChangeRate`

Pressure change rate is the ratio of the pressure change to the time during which the change occurred (value of pressure changes per unit time).

**Default Unit**: PascalPerSecond

**Units**:

- Pascal Per Second (`PascalPerSecond`).

  Abbreviation(s): Pa/s
- Pascal Per Minute (`PascalPerMinute`).

  Abbreviation(s): Pa/min
- Millimeter Of Mercury Per Second (`MillimeterOfMercuryPerSecond`).

  Abbreviation(s): mmHg/s
- Atmosphere Per Second (`AtmospherePerSecond`).

  Abbreviation(s): atm/s
- Pound Force Per Square Inch Per Second (`PoundForcePerSquareInchPerSecond`).

  Abbreviation(s): psi/s, lb/in²/s
- Pound Force Per Square Inch Per Minute (`PoundForcePerSquareInchPerMinute`).

  Abbreviation(s): psi/min, lb/in²/min
- Bar Per Second (`BarPerSecond`).

  Abbreviation(s): bar/s
- Bar Per Minute (`BarPerMinute`).

  Abbreviation(s): bar/min

### Rotational Acceleration
`RotationalAcceleration`

Angular acceleration is the rate of change of rotational speed.

**Default Unit**: RadianPerSecondSquared

**Units**:

- Radian Per Second Squared (`RadianPerSecondSquared`).

  Abbreviation(s): rad/s²
- Degree Per Second Squared (`DegreePerSecondSquared`).

  Abbreviation(s): °/s², deg/s²
- Revolution Per Minute Per Second (`RevolutionPerMinutePerSecond`).

  Abbreviation(s): rpm/s
- Revolution Per Second Squared (`RevolutionPerSecondSquared`).

  Abbreviation(s): r/s²

### Rotational Speed
`RotationalSpeed`

Rotational speed (sometimes called speed of revolution) is the number of complete rotations, revolutions, cycles, or turns per time unit. Rotational speed is a cyclic frequency, measured in radians per second or in hertz in the SI System by scientists, or in revolutions per minute (rpm or min-1) or revolutions per second in everyday life. The symbol for rotational speed is ω (the Greek lowercase letter "omega").

**Default Unit**: RadianPerSecond

**Units**:

- Radian Per Second (`RadianPerSecond`).

  Abbreviation(s): rad/s
- Degree Per Second (`DegreePerSecond`).

  Abbreviation(s): °/s, deg/s
- Degree Per Minute (`DegreePerMinute`).

  Abbreviation(s): °/min, deg/min
- Revolution Per Second (`RevolutionPerSecond`).

  Abbreviation(s): r/s
- Revolution Per Minute (`RevolutionPerMinute`).

  Abbreviation(s): rpm, r/min

### Rotational Stiffness
`RotationalStiffness`

https://en.wikipedia.org/wiki/Stiffness#Rotational_stiffness

**Default Unit**: NewtonMeterPerRadian

**Units**:

- Newton Meter Per Radian (`NewtonMeterPerRadian`).

  Abbreviation(s): N·m/rad, Nm/rad
- Pound Force Foot Per Degrees (`PoundForceFootPerDegrees`).

  Abbreviation(s): lbf·ft/deg
- Kilopound Force Foot Per Degrees (`KilopoundForceFootPerDegrees`).

  Abbreviation(s): kipf·ft/°, kip·ft/°g, k·ft/°, kipf·ft/deg, kip·ft/deg, k·ft/deg
- Newton Millimeter Per Degree (`NewtonMillimeterPerDegree`).

  Abbreviation(s): N·mm/deg, Nmm/deg, N·mm/°, Nmm/°
- Newton Meter Per Degree (`NewtonMeterPerDegree`).

  Abbreviation(s): N·m/deg, Nm/deg, N·m/°, Nm/°
- Newton Millimeter Per Radian (`NewtonMillimeterPerRadian`).

  Abbreviation(s): N·mm/rad, Nmm/rad
- Pound Force Feet Per Radian (`PoundForceFeetPerRadian`).

  Abbreviation(s): lbf·ft/rad

### Rotational Stiffness Per Length
`RotationalStiffnessPerLength`

https://en.wikipedia.org/wiki/Stiffness#Rotational_stiffness

**Default Unit**: NewtonMeterPerRadianPerMeter

**Units**:

- Newton Meter Per Radian Per Meter (`NewtonMeterPerRadianPerMeter`).

  Abbreviation(s): N·m/rad/m, Nm/rad/m
- Pound Force Foot Per Degrees Per Foot (`PoundForceFootPerDegreesPerFoot`).

  Abbreviation(s): lbf·ft/deg/ft
- Kilopound Force Foot Per Degrees Per Foot (`KilopoundForceFootPerDegreesPerFoot`).

  Abbreviation(s): kipf·ft/°/ft, kip·ft/°/ft, k·ft/°/ft, kipf·ft/deg/ft, kip·ft/deg/ft, k·ft/deg/ft

### Specific Fuel Consumption
`SpecificFuelConsumption`

SFC is the fuel efficiency of an engine design with respect to thrust output

**Default Unit**: GramPerKilonewtonSecond

**Units**:

- Pound Mass Per Pound Force Hour (`PoundMassPerPoundForceHour`).

  Abbreviation(s): lb/(lbf·h)
- Kilogram Per Kilogram Force Hour (`KilogramPerKilogramForceHour`).

  Abbreviation(s): kg/(kgf·h)
- Gram Per Kilonewton Second (`GramPerKilonewtonSecond`).

  Abbreviation(s): g/(kN·s)

### Specific Volume
`SpecificVolume`

In thermodynamics, the specific volume of a substance is the ratio of the substance's volume to its mass. It is the reciprocal of density and an intrinsic property of matter as well.

**Default Unit**: CubicMeterPerKilogram

**Units**:

- Cubic Meter Per Kilogram (`CubicMeterPerKilogram`).

  Abbreviation(s): m³/kg
- Cubic Foot Per Pound (`CubicFootPerPound`).

  Abbreviation(s): ft³/lb

### Specific Weight
`SpecificWeight`

The SpecificWeight, or more precisely, the volumetric weight density, of a substance is its weight per unit volume.

**Default Unit**: NewtonPerCubicMeter

**Units**:

- Newton Per Cubic Millimeter (`NewtonPerCubicMillimeter`).

  Abbreviation(s): N/mm³
- Newton Per Cubic Centimeter (`NewtonPerCubicCentimeter`).

  Abbreviation(s): N/cm³
- Newton Per Cubic Meter (`NewtonPerCubicMeter`).

  Abbreviation(s): N/m³
- Kilogram Force Per Cubic Millimeter (`KilogramForcePerCubicMillimeter`).

  Abbreviation(s): kgf/mm³
- Kilogram Force Per Cubic Centimeter (`KilogramForcePerCubicCentimeter`).

  Abbreviation(s): kgf/cm³
- Kilogram Force Per Cubic Meter (`KilogramForcePerCubicMeter`).

  Abbreviation(s): kgf/m³
- Pound Force Per Cubic Inch (`PoundForcePerCubicInch`).

  Abbreviation(s): lbf/in³
- Pound Force Per Cubic Foot (`PoundForcePerCubicFoot`).

  Abbreviation(s): lbf/ft³
- Tonne Force Per Cubic Millimeter (`TonneForcePerCubicMillimeter`).

  Abbreviation(s): tf/mm³
- Tonne Force Per Cubic Centimeter (`TonneForcePerCubicCentimeter`).

  Abbreviation(s): tf/cm³
- Tonne Force Per Cubic Meter (`TonneForcePerCubicMeter`).

  Abbreviation(s): tf/m³

### Speed
`Speed`

In everyday use and in kinematics, the speed of an object is the magnitude of its velocity (the rate of change of its position); it is thus a scalar quantity.[1] The average speed of an object in an interval of time is the distance travelled by the object divided by the duration of the interval;[2] the instantaneous speed is the limit of the average speed as the duration of the time interval approaches zero.

**Default Unit**: MeterPerSecond

**Units**:

- Meter Per Second (`MeterPerSecond`).

  Abbreviation(s): m/s
- Meter Per Minute (`MeterPerMinute`).

  Abbreviation(s): m/min
- Meter Per Hour (`MeterPerHour`).

  Abbreviation(s): m/h
- Foot Per Second (`FootPerSecond`).

  Abbreviation(s): ft/s
- Foot Per Minute (`FootPerMinute`).

  Abbreviation(s): ft/min
- Foot Per Hour (`FootPerHour`).

  Abbreviation(s): ft/h
- Us Survey Foot Per Second (`UsSurveyFootPerSecond`).

  Abbreviation(s): ftUS/s
- Us Survey Foot Per Minute (`UsSurveyFootPerMinute`).

  Abbreviation(s): ftUS/min
- Us Survey Foot Per Hour (`UsSurveyFootPerHour`).

  Abbreviation(s): ftUS/h
- Inch Per Second (`InchPerSecond`).

  Abbreviation(s): in/s
- Inch Per Minute (`InchPerMinute`).

  Abbreviation(s): in/min
- Inch Per Hour (`InchPerHour`).

  Abbreviation(s): in/h
- Yard Per Second (`YardPerSecond`).

  Abbreviation(s): yd/s
- Yard Per Minute (`YardPerMinute`).

  Abbreviation(s): yd/min
- Yard Per Hour (`YardPerHour`).

  Abbreviation(s): yd/h
- Knot (`Knot`).

  Abbreviation(s): kn, kt, knot, knots

  The knot, by definition, is a unit of speed equals to 1 nautical mile per hour, which is exactly 1852.000 metres per hour. The length of the internationally agreed nautical mile is 1852 m. The US adopted the international definition in 1954, the UK adopted the international nautical mile definition in 1970.
- Mile Per Hour (`MilePerHour`).

  Abbreviation(s): mph
- Mach (`Mach`).

  Abbreviation(s): M, Ma, MN, MACH

### Standard Volume Flow
`StandardVolumeFlow`

The molar flow rate of a gas corrected to standardized conditions of temperature and pressure thus representing a fixed number of moles of gas regardless of composition and actual flow conditions.

**Default Unit**: StandardCubicMeterPerSecond

**Units**:

- Standard Cubic Meter Per Second (`StandardCubicMeterPerSecond`).

  Abbreviation(s): Sm³/s
- Standard Cubic Meter Per Minute (`StandardCubicMeterPerMinute`).

  Abbreviation(s): Sm³/min
- Standard Cubic Meter Per Hour (`StandardCubicMeterPerHour`).

  Abbreviation(s): Sm³/h
- Standard Cubic Meter Per Day (`StandardCubicMeterPerDay`).

  Abbreviation(s): Sm³/d
- Standard Cubic Centimeter Per Minute (`StandardCubicCentimeterPerMinute`).

  Abbreviation(s): sccm
- Standard Liter Per Minute (`StandardLiterPerMinute`).

  Abbreviation(s): slm
- Standard Cubic Foot Per Second (`StandardCubicFootPerSecond`).

  Abbreviation(s): Sft³/s
- Standard Cubic Foot Per Minute (`StandardCubicFootPerMinute`).

  Abbreviation(s): scfm
- Standard Cubic Foot Per Hour (`StandardCubicFootPerHour`).

  Abbreviation(s): scfh

### Torque
`Torque`

Torque, moment or moment of force (see the terminology below), is the tendency of a force to rotate an object about an axis,[1] fulcrum, or pivot. Just as a force is a push or a pull, a torque can be thought of as a twist to an object. Mathematically, torque is defined as the cross product of the lever-arm distance and force, which tends to produce rotation. Loosely speaking, torque is a measure of the turning force on an object such as a bolt or a flywheel. For example, pushing or pulling the handle of a wrench connected to a nut or bolt produces a torque (turning force) that loosens or tightens the nut or bolt.

**Default Unit**: NewtonMeter

**Units**:

- Newton Millimeter (`NewtonMillimeter`).

  Abbreviation(s): N·mm
- Newton Centimeter (`NewtonCentimeter`).

  Abbreviation(s): N·cm
- Newton Meter (`NewtonMeter`).

  Abbreviation(s): N·m
- Poundal Foot (`PoundalFoot`).

  Abbreviation(s): pdl·ft
- Pound Force Inch (`PoundForceInch`).

  Abbreviation(s): lbf·in
- Pound Force Foot (`PoundForceFoot`).

  Abbreviation(s): lbf·ft
- Gram Force Millimeter (`GramForceMillimeter`).

  Abbreviation(s): gf·mm
- Gram Force Centimeter (`GramForceCentimeter`).

  Abbreviation(s): gf·cm
- Gram Force Meter (`GramForceMeter`).

  Abbreviation(s): gf·m
- Kilogram Force Millimeter (`KilogramForceMillimeter`).

  Abbreviation(s): kgf·mm
- Kilogram Force Centimeter (`KilogramForceCentimeter`).

  Abbreviation(s): kgf·cm
- Kilogram Force Meter (`KilogramForceMeter`).

  Abbreviation(s): kgf·m
- Tonne Force Millimeter (`TonneForceMillimeter`).

  Abbreviation(s): tf·mm
- Tonne Force Centimeter (`TonneForceCentimeter`).

  Abbreviation(s): tf·cm
- Tonne Force Meter (`TonneForceMeter`).

  Abbreviation(s): tf·m

### Torque Per Length
`TorquePerLength`

**Units**:


### Volume Flow
`VolumeFlow`

In physics and engineering, in particular fluid dynamics and hydrometry, the volumetric flow rate, (also known as volume flow rate, rate of fluid flow or volume velocity) is the volume of fluid which passes through a given surface per unit time. The SI unit is m³/s (cubic meters per second). In US Customary Units and British Imperial Units, volumetric flow rate is often expressed as ft³/s (cubic feet per second). It is usually represented by the symbol Q.

**Default Unit**: CubicMeterPerSecond

**Units**:

- Cubic Meter Per Second (`CubicMeterPerSecond`).

  Abbreviation(s): m³/s
- Cubic Meter Per Minute (`CubicMeterPerMinute`).

  Abbreviation(s): m³/min
- Cubic Meter Per Hour (`CubicMeterPerHour`).

  Abbreviation(s): m³/h
- Cubic Meter Per Day (`CubicMeterPerDay`).

  Abbreviation(s): m³/d
- Cubic Foot Per Second (`CubicFootPerSecond`).

  Abbreviation(s): ft³/s
- Cubic Foot Per Minute (`CubicFootPerMinute`).

  Abbreviation(s): ft³/min, CFM
- Cubic Foot Per Hour (`CubicFootPerHour`).

  Abbreviation(s): ft³/h, cf/hr
- Cubic Yard Per Second (`CubicYardPerSecond`).

  Abbreviation(s): yd³/s
- Cubic Yard Per Minute (`CubicYardPerMinute`).

  Abbreviation(s): yd³/min
- Cubic Yard Per Hour (`CubicYardPerHour`).

  Abbreviation(s): yd³/h
- Cubic Yard Per Day (`CubicYardPerDay`).

  Abbreviation(s): cy/day
- Million Us Gallon Per Day (`MillionUsGallonPerDay`).

  Abbreviation(s): MGD
- Us Gallon Per Day (`UsGallonPerDay`).

  Abbreviation(s): gpd, gal/d
- Liter Per Second (`LiterPerSecond`).

  Abbreviation(s): l/s, LPS
- Liter Per Minute (`LiterPerMinute`).

  Abbreviation(s): l/min, LPM
- Liter Per Hour (`LiterPerHour`).

  Abbreviation(s): l/h, LPH
- Liter Per Day (`LiterPerDay`).

  Abbreviation(s): l/day, l/d, LPD
- Us Gallon Per Second (`UsGallonPerSecond`).

  Abbreviation(s): gal (U.S.)/s
- Us Gallon Per Minute (`UsGallonPerMinute`).

  Abbreviation(s): gal (U.S.)/min, GPM
- Uk Gallon Per Day (`UkGallonPerDay`).

  Abbreviation(s): gal (U. K.)/d
- Uk Gallon Per Hour (`UkGallonPerHour`).

  Abbreviation(s): gal (imp.)/h
- Uk Gallon Per Minute (`UkGallonPerMinute`).

  Abbreviation(s): gal (imp.)/min
- Uk Gallon Per Second (`UkGallonPerSecond`).

  Abbreviation(s): gal (imp.)/s
- Kilous Gallon Per Minute (`KilousGallonPerMinute`).

  Abbreviation(s): kgal (U.S.)/min, KGPM
- Us Gallon Per Hour (`UsGallonPerHour`).

  Abbreviation(s): gal (U.S.)/h
- Cubic Decimeter Per Minute (`CubicDecimeterPerMinute`).

  Abbreviation(s): dm³/min
- Oil Barrel Per Day (`OilBarrelPerDay`).

  Abbreviation(s): bbl/d, BOPD
- Oil Barrel Per Minute (`OilBarrelPerMinute`).

  Abbreviation(s): bbl/min, bpm
- Oil Barrel Per Hour (`OilBarrelPerHour`).

  Abbreviation(s): bbl/hr, bph
- Oil Barrel Per Second (`OilBarrelPerSecond`).

  Abbreviation(s): bbl/s
- Cubic Millimeter Per Second (`CubicMillimeterPerSecond`).

  Abbreviation(s): mm³/s
- Acre Foot Per Second (`AcreFootPerSecond`).

  Abbreviation(s): af/s
- Acre Foot Per Minute (`AcreFootPerMinute`).

  Abbreviation(s): af/m
- Acre Foot Per Hour (`AcreFootPerHour`).

  Abbreviation(s): af/h
- Acre Foot Per Day (`AcreFootPerDay`).

  Abbreviation(s): af/d
- Cubic Centimeter Per Minute (`CubicCentimeterPerMinute`).

  Abbreviation(s): cm³/min

### Volume Flow Per Area
`VolumeFlowPerArea`

**Default Unit**: CubicMeterPerSecondPerSquareMeter

**Units**:

- Cubic Meter Per Second Per Square Meter (`CubicMeterPerSecondPerSquareMeter`).

  Abbreviation(s): m³/(s·m²)
- Cubic Foot Per Minute Per Square Foot (`CubicFootPerMinutePerSquareFoot`).

  Abbreviation(s): CFM/ft²

### Volume Per Length
`VolumePerLength`

Volume, typically of fluid, that a container can hold within a unit of length.

**Default Unit**: CubicMeterPerMeter

**Units**:

- Cubic Meter Per Meter (`CubicMeterPerMeter`).

  Abbreviation(s): m³/m
- Liter Per Meter (`LiterPerMeter`).

  Abbreviation(s): l/m
- Liter Per Kilometer (`LiterPerKilometer`).

  Abbreviation(s): l/km
- Liter Per Millimeter (`LiterPerMillimeter`).

  Abbreviation(s): l/mm
- Oil Barrel Per Foot (`OilBarrelPerFoot`).

  Abbreviation(s): bbl/ft
- Cubic Yard Per Foot (`CubicYardPerFoot`).

  Abbreviation(s): yd³/ft
- Cubic Yard Per Us Survey Foot (`CubicYardPerUsSurveyFoot`).

  Abbreviation(s): yd³/ftUS
- Us Gallon Per Mile (`UsGallonPerMile`).

  Abbreviation(s): gal (U.S.)/mi
- Imperial Gallon Per Mile (`ImperialGallonPerMile`).

  Abbreviation(s): gal (imp.)/mi

### Warping Moment Of Inertia
`WarpingMomentOfInertia`

A geometric property of an area that is used to determine the warping stress.

**Default Unit**: MeterToTheSixth

**Units**:

- Meter To The Sixth (`MeterToTheSixth`).

  Abbreviation(s): m⁶
- Decimeter To The Sixth (`DecimeterToTheSixth`).

  Abbreviation(s): dm⁶
- Centimeter To The Sixth (`CentimeterToTheSixth`).

  Abbreviation(s): cm⁶
- Millimeter To The Sixth (`MillimeterToTheSixth`).

  Abbreviation(s): mm⁶
- Foot To The Sixth (`FootToTheSixth`).

  Abbreviation(s): ft⁶
- Inch To The Sixth (`InchToTheSixth`).

  Abbreviation(s): in⁶

## Electrical & Magnetic

### Electric Admittance
`ElectricAdmittance`

Electric admittance is a measure of how easily a circuit or device will allow a current to flow by the combined effect of conductance and susceptance in a circuit. It is defined as the inverse of impedance. The SI unit of admittance is the siemens (symbol S).

**Default Unit**: Siemens

**Units**:

- Siemens (`Siemens`).

  Abbreviation(s): S
- Mho (`Mho`).

  Abbreviation(s): ℧

### Electric Apparent Energy
`ElectricApparentEnergy`

A unit for expressing the integral of apparent power over time, equal to the product of 1 volt-ampere and 1 hour, or to 3600 joules.

**Default Unit**: VoltampereHour

**Units**:

- Voltampere Hour (`VoltampereHour`).

  Abbreviation(s): VAh

### Electric Apparent Power
`ElectricApparentPower`

Power engineers measure apparent power as the magnitude of the vector sum of active and reactive power. It is the product of the root mean square voltage (in volts) and the root mean square current (in amperes).

**Default Unit**: Voltampere

**Units**:

- Voltampere (`Voltampere`).

  Abbreviation(s): VA

### Electric Capacitance
`ElectricCapacitance`

Capacitance is the capacity of a material object or device to store electric charge.

**Default Unit**: Farad

**Units**:

- Farad (`Farad`).

  Abbreviation(s): F

### Electric Charge
`ElectricCharge`

Electric charge is the physical property of matter that causes it to experience a force when placed in an electromagnetic field.

**Default Unit**: Coulomb

**Units**:

- Coulomb (`Coulomb`).

  Abbreviation(s): C
- Ampere Hour (`AmpereHour`).

  Abbreviation(s): A-h, Ah

### Electric Charge Density
`ElectricChargeDensity`

In electromagnetism, charge density is a measure of the amount of electric charge per volume.

**Default Unit**: CoulombPerCubicMeter

**Units**:

- Coulomb Per Cubic Meter (`CoulombPerCubicMeter`).

  Abbreviation(s): C/m³

### Electric Conductance
`ElectricConductance`

The electrical conductance of an object is a measure of the ease with which an electric current passes. Along with susceptance, it is one of two elements of admittance. Its reciprocal quantity is electrical resistance.

**Default Unit**: Siemens

**Units**:

- Siemens (`Siemens`).

  Abbreviation(s): S
- Mho (`Mho`).

  Abbreviation(s): ℧

### Electric Conductivity
`ElectricConductivity`

Electrical conductivity or specific conductance is the reciprocal of electrical resistivity, and measures a material's ability to conduct an electric current.

**Default Unit**: SiemensPerMeter

**Units**:

- Siemens Per Meter (`SiemensPerMeter`).

  Abbreviation(s): S/m
- Siemens Per Inch (`SiemensPerInch`).

  Abbreviation(s): S/in
- Siemens Per Foot (`SiemensPerFoot`).

  Abbreviation(s): S/ft
- Siemens Per Centimeter (`SiemensPerCentimeter`).

  Abbreviation(s): S/cm

### Electric Current
`ElectricCurrent`

An electric current is a flow of electric charge. In electric circuits this charge is often carried by moving electrons in a wire. It can also be carried by ions in an electrolyte, or by both ions and electrons such as in a plasma.

**Default Unit**: Ampere

**Units**:

- Ampere (`Ampere`).

  Abbreviation(s): A

### Electric Current Density
`ElectricCurrentDensity`

In electromagnetism, current density is the electric current per unit area of cross section.

**Default Unit**: AmperePerSquareMeter

**Units**:

- Ampere Per Square Meter (`AmperePerSquareMeter`).

  Abbreviation(s): A/m²
- Ampere Per Square Inch (`AmperePerSquareInch`).

  Abbreviation(s): A/in²
- Ampere Per Square Foot (`AmperePerSquareFoot`).

  Abbreviation(s): A/ft²

### Electric Current Gradient
`ElectricCurrentGradient`

In electromagnetism, the current gradient describes how the current changes in time.

**Default Unit**: AmperePerSecond

**Units**:

- Ampere Per Second (`AmperePerSecond`).

  Abbreviation(s): A/s
- Ampere Per Minute (`AmperePerMinute`).

  Abbreviation(s): A/min
- Ampere Per Millisecond (`AmperePerMillisecond`).

  Abbreviation(s): A/ms
- Ampere Per Microsecond (`AmperePerMicrosecond`).

  Abbreviation(s): A/μs
- Ampere Per Nanosecond (`AmperePerNanosecond`).

  Abbreviation(s): A/ns

### Electric Field
`ElectricField`

An electric field is a force field that surrounds electric charges that attracts or repels other electric charges.

**Default Unit**: VoltPerMeter

**Units**:

- Volt Per Meter (`VoltPerMeter`).

  Abbreviation(s): V/m

### Electric Impedance
`ElectricImpedance`

Electric impedance is the opposition to alternating current presented by the combined effect of resistance and reactance in a circuit. It is defined as the inverse of admittance. The SI unit of impedance is the ohm (symbol Ω).

**Default Unit**: Ohm

**Units**:

- Ohm (`Ohm`).

  Abbreviation(s): Ω

### Electric Inductance
`ElectricInductance`

Inductance is a property of an electrical conductor which opposes a change in current.

**Default Unit**: Henry

**Units**:

- Henry (`Henry`).

  Abbreviation(s): H

### Electric Potential
`ElectricPotential`

In classical electromagnetism, the electric potential (a scalar quantity denoted by Φ, ΦE or V and also called the electric field potential or the electrostatic potential) at a point is the amount of electric potential energy that a unitary point charge would have when located at that point.

**Default Unit**: Volt

**Units**:

- Volt (`Volt`).

  Abbreviation(s): V

### Electric Potential Change Rate
`ElectricPotentialChangeRate`

ElectricPotential change rate is the ratio of the electric potential change to the time during which the change occurred (value of electric potential changes per unit time).

**Default Unit**: VoltPerSecond

**Units**:

- Volt Per Second (`VoltPerSecond`).

  Abbreviation(s): V/s
- Volt Per Microsecond (`VoltPerMicrosecond`).

  Abbreviation(s): V/μs
- Volt Per Minute (`VoltPerMinute`).

  Abbreviation(s): V/min
- Volt Per Hour (`VoltPerHour`).

  Abbreviation(s): V/h

### Electric Reactance
`ElectricReactance`

In electrical circuits, reactance is the opposition presented to alternating current by inductance and capacitance. Along with resistance, it is one of two elements of impedance.

**Default Unit**: Ohm

**Units**:

- Ohm (`Ohm`).

  Abbreviation(s): Ω

### Electric Reactive Energy
`ElectricReactiveEnergy`

The volt-ampere reactive hour (expressed as varh) is the reactive power of one Volt-ampere reactive produced in one hour.

**Default Unit**: VoltampereReactiveHour

**Units**:

- Voltampere Reactive Hour (`VoltampereReactiveHour`).

  Abbreviation(s): varh

### Electric Reactive Power
`ElectricReactivePower`

In electric power transmission and distribution, volt-ampere reactive (var) is a unit of measurement of reactive power. Reactive power exists in an AC circuit when the current and voltage are not in phase.

**Default Unit**: VoltampereReactive

**Units**:

- Voltampere Reactive (`VoltampereReactive`).

  Abbreviation(s): var

### Electric Resistance
`ElectricResistance`

The electrical resistance of an object is a measure of its opposition to the flow of electric current. Along with reactance, it is one of two elements of impedance. Its reciprocal quantity is electrical conductance.

**Default Unit**: Ohm

**Units**:

- Ohm (`Ohm`).

  Abbreviation(s): Ω

### Electric Resistivity
`ElectricResistivity`

Electrical resistivity (also known as resistivity, specific electrical resistance, or volume resistivity) is a fundamental property that quantifies how strongly a given material opposes the flow of electric current.

**Default Unit**: OhmMeter

**Units**:

- Ohm Meter (`OhmMeter`).

  Abbreviation(s): Ω·m
- Ohm Centimeter (`OhmCentimeter`).

  Abbreviation(s): Ω·cm

### Electric Surface Charge Density
`ElectricSurfaceChargeDensity`

In electromagnetism, surface charge density is a measure of the amount of electric charge per surface area.

**Default Unit**: CoulombPerSquareMeter

**Units**:

- Coulomb Per Square Meter (`CoulombPerSquareMeter`).

  Abbreviation(s): C/m²
- Coulomb Per Square Centimeter (`CoulombPerSquareCentimeter`).

  Abbreviation(s): C/cm²
- Coulomb Per Square Inch (`CoulombPerSquareInch`).

  Abbreviation(s): C/in²

### Electric Susceptance
`ElectricSusceptance`

Electrical susceptance is the imaginary part of admittance, where the real part is conductance.

**Default Unit**: Siemens

**Units**:

- Siemens (`Siemens`).

  Abbreviation(s): S
- Mho (`Mho`).

  Abbreviation(s): ℧

### Magnetic Field
`MagneticField`

A magnetic field is a force field that is created by moving electric charges (electric currents) and magnetic dipoles, and exerts a force on other nearby moving charges and magnetic dipoles.

**Default Unit**: Tesla

**Units**:

- Tesla (`Tesla`).

  Abbreviation(s): T
- Gauss (`Gauss`).

  Abbreviation(s): G

### Magnetic Flux
`MagneticFlux`

In physics, specifically electromagnetism, the magnetic flux through a surface is the surface integral of the normal component of the magnetic field B passing through that surface.

**Default Unit**: Weber

**Units**:

- Weber (`Weber`).

  Abbreviation(s): Wb

### Magnetization
`Magnetization`

In classical electromagnetism, magnetization is the vector field that expresses the density of permanent or induced magnetic dipole moments in a magnetic material.

**Default Unit**: AmperePerMeter

**Units**:

- Ampere Per Meter (`AmperePerMeter`).

  Abbreviation(s): A/m

### Permeability
`Permeability`

In electromagnetism, permeability is the measure of the ability of a material to support the formation of a magnetic field within itself.

**Default Unit**: HenryPerMeter

**Units**:

- Henry Per Meter (`HenryPerMeter`).

  Abbreviation(s): H/m

### Permittivity
`Permittivity`

In electromagnetism, permittivity is the measure of resistance that is encountered when forming an electric field in a particular medium.

**Default Unit**: FaradPerMeter

**Units**:

- Farad Per Meter (`FaradPerMeter`).

  Abbreviation(s): F/m

## Thermal

### Coefficient Of Thermal Expansion
`CoefficientOfThermalExpansion`

A unit that represents a fractional change in size in response to a change in temperature.

**Default Unit**: PerKelvin

**Units**:

- Per Kelvin (`PerKelvin`).

  Abbreviation(s): K⁻¹
- Per Degree Celsius (`PerDegreeCelsius`).

  Abbreviation(s): °C⁻¹
- Per Degree Fahrenheit (`PerDegreeFahrenheit`).

  Abbreviation(s): °F⁻¹
- Ppm Per Kelvin (`PpmPerKelvin`).

  Abbreviation(s): ppm/K
- Ppm Per Degree Celsius (`PpmPerDegreeCelsius`).

  Abbreviation(s): ppm/°C
- Ppm Per Degree Fahrenheit (`PpmPerDegreeFahrenheit`).

  Abbreviation(s): ppm/°F

### Energy
`Energy`

The joule, symbol J, is a derived unit of energy, work, or amount of heat in the International System of Units. It is equal to the energy transferred (or work done) when applying a force of one newton through a distance of one metre (1 newton metre or N·m), or in passing an electric current of one ampere through a resistance of one ohm for one second. Many other units of energy are included. Please do not confuse this definition of the calorie with the one colloquially used by the food industry, the large calorie, which is equivalent to 1 kcal. Thermochemical definition of the calorie is used. For BTU, the IT definition is used.

**Default Unit**: Joule

**Units**:

- Joule (`Joule`).

  Abbreviation(s): J
- Calorie (`Calorie`).

  Abbreviation(s): cal
- British Thermal Unit (`BritishThermalUnit`).

  Abbreviation(s): BTU
- Electron Volt (`ElectronVolt`).

  Abbreviation(s): eV

  In physics, an electronvolt (symbol eV, also written electron-volt and electron volt) is the measure of an amount of kinetic energy gained by a single electron accelerating from rest through an electric potential difference of one volt in vacuum. When used as a unit of energy, the numerical value of 1 eV in joules (symbol J) is equivalent to the numerical value of the charge of an electron in coulombs (symbol C). Under the 2019 redefinition of the SI base units, this sets 1 eV equal to the exact value 1.602176634×10−19 J.
- Foot Pound (`FootPound`).

  Abbreviation(s): ft·lb

  A pound-foot (lb⋅ft), abbreviated from pound-force foot (lbf · ft), is a unit of torque representing one pound of force acting at a perpendicular distance of one foot from a pivot point. Conversely one foot pound-force (ft · lbf) is the moment about an axis that applies one pound-force at a radius of one foot.
- Erg (`Erg`).

  Abbreviation(s): erg

  The erg is a unit of energy equal to 10−7 joules (100 nJ). It originated in the Centimetre–gram–second system of units (CGS). It has the symbol erg. The erg is not an SI unit. Its name is derived from ergon (ἔργον), a Greek word meaning 'work' or 'task'.
- Watt Hour (`WattHour`).

  Abbreviation(s): Wh
- Watt Day (`WattDay`).

  Abbreviation(s): Wd
- Therm Ec (`ThermEc`).

  Abbreviation(s): th (E.C.)

  The therm (symbol, thm) is a non-SI unit of heat energy equal to 100,000 British thermal units (BTU), and approximately 105 megajoules, 29.3 kilowatt-hours, 25,200 kilocalories and 25.2 thermies. One therm is the energy content of approximately 100 cubic feet (2.83 cubic metres) of natural gas at standard temperature and pressure. However, the BTU is not standardised worldwide, with slightly different values in the EU, UK, and United States, meaning that the energy content of the therm also varies by territory.
- Therm Us (`ThermUs`).

  Abbreviation(s): th (U.S.)

  The therm (symbol, thm) is a non-SI unit of heat energy equal to 100,000 British thermal units (BTU), and approximately 105 megajoules, 29.3 kilowatt-hours, 25,200 kilocalories and 25.2 thermies. One therm is the energy content of approximately 100 cubic feet (2.83 cubic metres) of natural gas at standard temperature and pressure. However, the BTU is not standardised worldwide, with slightly different values in the EU, UK, and United States, meaning that the energy content of the therm also varies by territory.
- Therm Imperial (`ThermImperial`).

  Abbreviation(s): th (imp.)

  The therm (symbol, thm) is a non-SI unit of heat energy equal to 100,000 British thermal units (BTU), and approximately 105 megajoules, 29.3 kilowatt-hours, 25,200 kilocalories and 25.2 thermies. One therm is the energy content of approximately 100 cubic feet (2.83 cubic metres) of natural gas at standard temperature and pressure. However, the BTU is not standardised worldwide, with slightly different values in the EU, UK, and United States, meaning that the energy content of the therm also varies by territory.
- Horsepower Hour (`HorsepowerHour`).

  Abbreviation(s): hp·h

  A horsepower-hour (symbol: hp⋅h) is an outdated unit of energy, not used in the International System of Units. The unit represents an amount of work a horse is supposed capable of delivering during an hour (1 horsepower integrated over a time interval of an hour).

### Energy Density
`EnergyDensity`

**Default Unit**: JoulePerCubicMeter

**Units**:

- Joule Per Cubic Meter (`JoulePerCubicMeter`).

  Abbreviation(s): J/m³
- Watt Hour Per Cubic Meter (`WattHourPerCubicMeter`).

  Abbreviation(s): Wh/m³

### Entropy
`Entropy`

Entropy is an important concept in the branch of science known as thermodynamics. The idea of "irreversibility" is central to the understanding of entropy.  It is often said that entropy is an expression of the disorder, or randomness of a system, or of our lack of information about it. Entropy is an extensive property. It has the dimension of energy divided by temperature, which has a unit of joules per kelvin (J/K) in the International System of Units

**Default Unit**: JoulePerKelvin

**Units**:

- Joule Per Kelvin (`JoulePerKelvin`).

  Abbreviation(s): J/K
- Calorie Per Kelvin (`CaloriePerKelvin`).

  Abbreviation(s): cal/K
- Joule Per Degree Celsius (`JoulePerDegreeCelsius`).

  Abbreviation(s): J/°C

### Heat Flux
`HeatFlux`

Heat flux is the flow of energy per unit of area per unit of time

**Default Unit**: WattPerSquareMeter

**Units**:

- Watt Per Square Meter (`WattPerSquareMeter`).

  Abbreviation(s): W/m²
- Watt Per Square Inch (`WattPerSquareInch`).

  Abbreviation(s): W/in²
- Watt Per Square Foot (`WattPerSquareFoot`).

  Abbreviation(s): W/ft²
- Btu Per Second Square Inch (`BtuPerSecondSquareInch`).

  Abbreviation(s): BTU/(s·in²)
- Btu Per Second Square Foot (`BtuPerSecondSquareFoot`).

  Abbreviation(s): BTU/(s·ft²)
- Btu Per Minute Square Foot (`BtuPerMinuteSquareFoot`).

  Abbreviation(s): BTU/(min·ft²)
- Btu Per Hour Square Foot (`BtuPerHourSquareFoot`).

  Abbreviation(s): BTU/(h·ft²)
- Calorie Per Second Square Centimeter (`CaloriePerSecondSquareCentimeter`).

  Abbreviation(s): cal/(s·cm²)
- Kilocalorie Per Hour Square Meter (`KilocaloriePerHourSquareMeter`).

  Abbreviation(s): kcal/(h·m²)
- Pound Force Per Foot Second (`PoundForcePerFootSecond`).

  Abbreviation(s): lbf/(ft·s)
- Pound Per Second Cubed (`PoundPerSecondCubed`).

  Abbreviation(s): lb/s³, lbm/s³

### Heat Transfer Coefficient
`HeatTransferCoefficient`

The heat transfer coefficient or film coefficient, or film effectiveness, in thermodynamics and in mechanics is the proportionality constant between the heat flux and the thermodynamic driving force for the flow of heat (i.e., the temperature difference, ΔT)

**Default Unit**: WattPerSquareMeterKelvin

**Units**:

- Watt Per Square Meter Kelvin (`WattPerSquareMeterKelvin`).

  Abbreviation(s): W/(m²·K)
- Watt Per Square Meter Celsius (`WattPerSquareMeterCelsius`).

  Abbreviation(s): W/(m²·°C)
- Btu Per Hour Square Foot Degree Fahrenheit (`BtuPerHourSquareFootDegreeFahrenheit`).

  Abbreviation(s): Btu/(h·ft²·°F), Btu/(ft²·h·°F), Btu/(hr·ft²·°F), Btu/(ft²·hr·°F)
- Calorie Per Hour Square Meter Degree Celsius (`CaloriePerHourSquareMeterDegreeCelsius`).

  Abbreviation(s): kcal/(h·m²·°C), kcal/(m²·h·°C), kcal/(hr·m²·°C), kcal/(m²·hr·°C)

### Molar Energy
`MolarEnergy`

Molar energy is the amount of energy stored in 1 mole of a substance.

**Default Unit**: JoulePerMole

**Units**:

- Joule Per Mole (`JoulePerMole`).

  Abbreviation(s): J/mol

### Molar Entropy
`MolarEntropy`

Molar entropy is amount of energy required to increase temperature of 1 mole substance by 1 Kelvin.

**Default Unit**: JoulePerMoleKelvin

**Units**:

- Joule Per Mole Kelvin (`JoulePerMoleKelvin`).

  Abbreviation(s): J/(mol·K)

### Specific Energy
`SpecificEnergy`

The SpecificEnergy

**Default Unit**: JoulePerKilogram

**Units**:

- Joule Per Kilogram (`JoulePerKilogram`).

  Abbreviation(s): J/kg
- Mega Joule Per Tonne (`MegaJoulePerTonne`).

  Abbreviation(s): MJ/t
- Calorie Per Gram (`CaloriePerGram`).

  Abbreviation(s): cal/g
- Watt Hour Per Kilogram (`WattHourPerKilogram`).

  Abbreviation(s): Wh/kg
- Watt Day Per Kilogram (`WattDayPerKilogram`).

  Abbreviation(s): Wd/kg
- Watt Day Per Tonne (`WattDayPerTonne`).

  Abbreviation(s): Wd/t
- Watt Day Per Short Ton (`WattDayPerShortTon`).

  Abbreviation(s): Wd/ST
- Watt Hour Per Pound (`WattHourPerPound`).

  Abbreviation(s): Wh/lbs
- Btu Per Pound (`BtuPerPound`).

  Abbreviation(s): btu/lb

### Specific Entropy
`SpecificEntropy`

Specific entropy is an amount of energy required to raise temperature of a substance by 1 Kelvin per unit mass.

**Default Unit**: JoulePerKilogramKelvin

**Units**:

- Joule Per Kilogram Kelvin (`JoulePerKilogramKelvin`).

  Abbreviation(s): J/kg·K
- Joule Per Kilogram Degree Celsius (`JoulePerKilogramDegreeCelsius`).

  Abbreviation(s): J/kg·°C
- Calorie Per Gram Kelvin (`CaloriePerGramKelvin`).

  Abbreviation(s): cal/g·K
- Btu Per Pound Fahrenheit (`BtuPerPoundFahrenheit`).

  Abbreviation(s): BTU/(lb·°F), BTU/(lbm·°F)

### Temperature
`Temperature`

A temperature is a numerical measure of hot or cold. Its measurement is by detection of heat radiation or particle velocity or kinetic energy, or by the bulk behavior of a thermometric material. It may be calibrated in any of various temperature scales, Celsius, Fahrenheit, Kelvin, etc. The fundamental physical definition of temperature is provided by thermodynamics.

**Default Unit**: Kelvin

**Units**:

- Kelvin (`Kelvin`).

  Abbreviation(s): K
- Degree Celsius (`DegreeCelsius`).

  Abbreviation(s): °C
- Millidegree Celsius (`MillidegreeCelsius`).

  Abbreviation(s): m°C
- Degree Delisle (`DegreeDelisle`).

  Abbreviation(s): °De
- Degree Fahrenheit (`DegreeFahrenheit`).

  Abbreviation(s): °F
- Degree Newton (`DegreeNewton`).

  Abbreviation(s): °N
- Degree Rankine (`DegreeRankine`).

  Abbreviation(s): °R
- Degree Reaumur (`DegreeReaumur`).

  Abbreviation(s): °Ré
- Degree Roemer (`DegreeRoemer`).

  Abbreviation(s): °Rø
- Solar Temperature (`SolarTemperature`).

  Abbreviation(s): T⊙

### Temperature Change Rate
`TemperatureChangeRate`

Temperature change rate is the ratio of the temperature change to the time during which the change occurred (value of temperature changes per unit time).

**Default Unit**: DegreeCelsiusPerSecond

**Units**:

- Degree Celsius Per Second (`DegreeCelsiusPerSecond`).

  Abbreviation(s): °C/s
- Degree Celsius Per Minute (`DegreeCelsiusPerMinute`).

  Abbreviation(s): °C/min
- Degree Kelvin Per Minute (`DegreeKelvinPerMinute`).

  Abbreviation(s): K/min
- Degree Fahrenheit Per Minute (`DegreeFahrenheitPerMinute`).

  Abbreviation(s): °F/min
- Degree Fahrenheit Per Second (`DegreeFahrenheitPerSecond`).

  Abbreviation(s): °F/s
- Degree Kelvin Per Second (`DegreeKelvinPerSecond`).

  Abbreviation(s): K/s
- Degree Celsius Per Hour (`DegreeCelsiusPerHour`).

  Abbreviation(s): °C/h
- Degree Kelvin Per Hour (`DegreeKelvinPerHour`).

  Abbreviation(s): K/h
- Degree Fahrenheit Per Hour (`DegreeFahrenheitPerHour`).

  Abbreviation(s): °F/h

### Temperature Delta
`TemperatureDelta`

Difference between two temperatures. The conversions are different than for Temperature.

**Default Unit**: Kelvin

**Units**:

- Kelvin (`Kelvin`).

  Abbreviation(s): ∆K
- Degree Celsius (`DegreeCelsius`).

  Abbreviation(s): ∆°C
- Degree Delisle (`DegreeDelisle`).

  Abbreviation(s): ∆°De
- Degree Fahrenheit (`DegreeFahrenheit`).

  Abbreviation(s): ∆°F
- Degree Newton (`DegreeNewton`).

  Abbreviation(s): ∆°N
- Degree Rankine (`DegreeRankine`).

  Abbreviation(s): ∆°R
- Degree Reaumur (`DegreeReaumur`).

  Abbreviation(s): ∆°Ré
- Degree Roemer (`DegreeRoemer`).

  Abbreviation(s): ∆°Rø

### Temperature Gradient
`TemperatureGradient`

**Default Unit**: KelvinPerMeter

**Units**:

- Kelvin Per Meter (`KelvinPerMeter`).

  Abbreviation(s): ∆°K/m
- Degree Celsius Per Meter (`DegreeCelsiusPerMeter`).

  Abbreviation(s): ∆°C/m
- Degree Fahrenheit Per Foot (`DegreeFahrenheitPerFoot`).

  Abbreviation(s): ∆°F/ft
- Degree Celsius Per Kilometer (`DegreeCelsiusPerKilometer`).

  Abbreviation(s): ∆°C/km

### Thermal Conductivity
`ThermalConductivity`

Thermal conductivity is the property of a material to conduct heat.

**Default Unit**: WattPerMeterKelvin

**Units**:

- Watt Per Meter Kelvin (`WattPerMeterKelvin`).

  Abbreviation(s): W/(m·K)
- Btu Per Hour Foot Fahrenheit (`BtuPerHourFootFahrenheit`).

  Abbreviation(s): BTU/(h·ft·°F)

### Thermal Resistance
`ThermalResistance`

**Units**:


### Volumetric Heat Capacity
`VolumetricHeatCapacity`

The volumetric heat capacity is the amount of energy that must be added, in the form of heat, to one unit of volume of the material in order to cause an increase of one unit in its temperature.

**Default Unit**: JoulePerCubicMeterKelvin

**Units**:

- Joule Per Cubic Meter Kelvin (`JoulePerCubicMeterKelvin`).

  Abbreviation(s): J/(m³·K)
- Joule Per Cubic Meter Degree Celsius (`JoulePerCubicMeterDegreeCelsius`).

  Abbreviation(s): J/(m³·°C)
- Calorie Per Cubic Centimeter Degree Celsius (`CaloriePerCubicCentimeterDegreeCelsius`).

  Abbreviation(s): cal/(cm³·°C)
- Btu Per Cubic Foot Degree Fahrenheit (`BtuPerCubicFootDegreeFahrenheit`).

  Abbreviation(s): BTU/(ft³·°F)

## Light & Radiation

### Absorbed Dose Of Ionizing Radiation
`AbsorbedDoseOfIonizingRadiation`

Absorbed dose is a dose quantity which is the measure of the energy deposited in matter by ionizing radiation per unit mass.

**Default Unit**: Gray

**Units**:

- Gray (`Gray`).

  Abbreviation(s): Gy

  The gray is the unit of ionizing radiation dose in the SI, defined as the absorption of one joule of radiation energy per kilogram of matter.
- Rad (`Rad`).

  Abbreviation(s): rad

  The rad is a unit of absorbed radiation dose, defined as 1 rad = 0.01 Gy = 0.01 J/kg.

### Dose Area Product
`DoseAreaProduct`

It is defined as the absorbed dose multiplied by the area irradiated.

**Default Unit**: GraySquareMeter

**Units**:

- Gray Square Meter (`GraySquareMeter`).

  Abbreviation(s): Gy·m²
- Gray Square Decimeter (`GraySquareDecimeter`).

  Abbreviation(s): Gy·dm²
- Gray Square Centimeter (`GraySquareCentimeter`).

  Abbreviation(s): Gy·cm²
- Gray Square Millimeter (`GraySquareMillimeter`).

  Abbreviation(s): Gy·mm²

### Illuminance
`Illuminance`

In photometry, illuminance is the total luminous flux incident on a surface, per unit area.

**Default Unit**: Lux

**Units**:

- Lux (`Lux`).

  Abbreviation(s): lx

### Irradiance
`Irradiance`

Irradiance is the intensity of ultraviolet (UV) or visible light incident on a surface.

**Default Unit**: WattPerSquareMeter

**Units**:

- Watt Per Square Meter (`WattPerSquareMeter`).

  Abbreviation(s): W/m²
- Watt Per Square Centimeter (`WattPerSquareCentimeter`).

  Abbreviation(s): W/cm²

### Irradiation
`Irradiation`

Irradiation is the process by which an object is exposed to radiation. The exposure can originate from various sources, including natural sources.

**Default Unit**: JoulePerSquareMeter

**Units**:

- Joule Per Square Meter (`JoulePerSquareMeter`).

  Abbreviation(s): J/m²
- Joule Per Square Centimeter (`JoulePerSquareCentimeter`).

  Abbreviation(s): J/cm²
- Joule Per Square Millimeter (`JoulePerSquareMillimeter`).

  Abbreviation(s): J/mm²
- Watt Hour Per Square Meter (`WattHourPerSquareMeter`).

  Abbreviation(s): Wh/m²
- Btu Per Square Foot (`BtuPerSquareFoot`).

  Abbreviation(s): Btu/ft²

### Luminance
`Luminance`

**Default Unit**: CandelaPerSquareMeter

**Units**:

- Candela Per Square Meter (`CandelaPerSquareMeter`).

  Abbreviation(s): Cd/m²
- Candela Per Square Foot (`CandelaPerSquareFoot`).

  Abbreviation(s): Cd/ft²
- Candela Per Square Inch (`CandelaPerSquareInch`).

  Abbreviation(s): Cd/in²
- Nit (`Nit`).

  Abbreviation(s): nt

### Luminosity
`Luminosity`

Luminosity is an absolute measure of radiated electromagnetic power (light), the radiant power emitted by a light-emitting object.

**Default Unit**: Watt

**Units**:

- Watt (`Watt`).

  Abbreviation(s): W
- Solar Luminosity (`SolarLuminosity`).

  Abbreviation(s): L⊙

  The IAU has defined a nominal solar luminosity of 3.828×10^26 W to promote publication of consistent and comparable values in units of the solar luminosity.

### Luminous Flux
`LuminousFlux`

In photometry, luminous flux or luminous power is the measure of the perceived power of light.

**Default Unit**: Lumen

**Units**:

- Lumen (`Lumen`).

  Abbreviation(s): lm

### Luminous Intensity
`LuminousIntensity`

In photometry, luminous intensity is a measure of the wavelength-weighted power emitted by a light source in a particular direction per unit solid angle, based on the luminosity function, a standardized model of the sensitivity of the human eye.

**Default Unit**: Candela

**Units**:

- Candela (`Candela`).

  Abbreviation(s): cd

### Radiation Equivalent Dose
`RadiationEquivalentDose`

Equivalent dose is a dose quantity representing the stochastic health effects of low levels of ionizing radiation on the human body which represents the probability of radiation-induced cancer and genetic damage.

**Default Unit**: Sievert

**Units**:

- Sievert (`Sievert`).

  Abbreviation(s): Sv

  The sievert is a unit in the International System of Units (SI) intended to represent the stochastic health risk of ionizing radiation, which is defined as the probability of causing radiation-induced cancer and genetic damage.
- Roentgen Equivalent Man (`RoentgenEquivalentMan`).

  Abbreviation(s): rem

### Radiation Equivalent Dose Rate
`RadiationEquivalentDoseRate`

A dose rate is quantity of radiation absorbed or delivered per unit time.

**Default Unit**: SievertPerSecond

**Units**:

- Sievert Per Hour (`SievertPerHour`).

  Abbreviation(s): Sv/h
- Sievert Per Second (`SievertPerSecond`).

  Abbreviation(s): Sv/s
- Roentgen Equivalent Man Per Hour (`RoentgenEquivalentManPerHour`).

  Abbreviation(s): rem/h

### Radiation Exposure
`RadiationExposure`

Radiation exposure is a measure of the ionization of air due to ionizing radiation from photons.

**Default Unit**: CoulombPerKilogram

**Units**:

- Coulomb Per Kilogram (`CoulombPerKilogram`).

  Abbreviation(s): C/kg
- Roentgen (`Roentgen`).

  Abbreviation(s): R

### Radioactivity
`Radioactivity`

Amount of ionizing radiation released when an element spontaneously emits energy as a result of the radioactive decay of an unstable atom per unit time.

**Default Unit**: Becquerel

**Units**:

- Becquerel (`Becquerel`).

  Abbreviation(s): Bq

  Activity of a quantity of radioactive material in which one nucleus decays per second.
- Curie (`Curie`).

  Abbreviation(s): Ci
- Rutherford (`Rutherford`).

  Abbreviation(s): Rd

  Activity of a quantity of radioactive material in which one million nuclei decay per second.

## Chemical & Material

### Mass Concentration
`MassConcentration`

In chemistry, the mass concentration ρi (or γi) is defined as the mass of a constituent mi divided by the volume of the mixture V

**Default Unit**: KilogramPerCubicMeter

**Units**:

- Gram Per Cubic Millimeter (`GramPerCubicMillimeter`).

  Abbreviation(s): g/mm³
- Gram Per Cubic Centimeter (`GramPerCubicCentimeter`).

  Abbreviation(s): g/cm³
- Gram Per Cubic Meter (`GramPerCubicMeter`).

  Abbreviation(s): g/m³
- Gram Per Microliter (`GramPerMicroliter`).

  Abbreviation(s): g/μl
- Gram Per Milliliter (`GramPerMilliliter`).

  Abbreviation(s): g/ml
- Gram Per Deciliter (`GramPerDeciliter`).

  Abbreviation(s): g/dl
- Gram Per Liter (`GramPerLiter`).

  Abbreviation(s): g/l
- Tonne Per Cubic Millimeter (`TonnePerCubicMillimeter`).

  Abbreviation(s): t/mm³
- Tonne Per Cubic Centimeter (`TonnePerCubicCentimeter`).

  Abbreviation(s): t/cm³
- Tonne Per Cubic Meter (`TonnePerCubicMeter`).

  Abbreviation(s): t/m³
- Pound Per Cubic Inch (`PoundPerCubicInch`).

  Abbreviation(s): lb/in³
- Pound Per Cubic Foot (`PoundPerCubicFoot`).

  Abbreviation(s): lb/ft³
- Slug Per Cubic Foot (`SlugPerCubicFoot`).

  Abbreviation(s): slug/ft³
- Pound Per U S Gallon (`PoundPerUSGallon`).

  Abbreviation(s): ppg (U.S.)
- Ounce Per U S Gallon (`OuncePerUSGallon`).

  Abbreviation(s): oz/gal (U.S.)
- Ounce Per Imperial Gallon (`OuncePerImperialGallon`).

  Abbreviation(s): oz/gal (imp.)
- Pound Per Imperial Gallon (`PoundPerImperialGallon`).

  Abbreviation(s): ppg (imp.)

### Molality
`Molality`

Molality is a measure of the amount of solute in a solution relative to a given mass of solvent.

**Default Unit**: MolePerKilogram

**Units**:

- Mole Per Kilogram (`MolePerKilogram`).

  Abbreviation(s): mol/kg
- Mole Per Gram (`MolePerGram`).

  Abbreviation(s): mol/g

### Molarity
`Molarity`

Molar concentration, also called molarity, amount concentration or substance concentration, is a measure of the concentration of a solute in a solution, or of any chemical species, in terms of amount of substance in a given volume.

**Default Unit**: MolePerCubicMeter

**Units**:

- Mole Per Cubic Meter (`MolePerCubicMeter`).

  Abbreviation(s): mol/m³
- Mole Per Liter (`MolePerLiter`).

  Abbreviation(s): mol/l, M
- Pound Mole Per Cubic Foot (`PoundMolePerCubicFoot`).

  Abbreviation(s): lbmol/ft³

### Porous Medium Permeability
`PorousMediumPermeability`

**Default Unit**: SquareMeter

**Units**:

- Darcy (`Darcy`).

  Abbreviation(s): D

  The darcy (or darcy unit) and millidarcy (md or mD) are units of permeability, named after Henry Darcy. They are not SI units, but they are widely used in petroleum engineering and geology.
- Square Meter (`SquareMeter`).

  Abbreviation(s): m²
- Square Centimeter (`SquareCentimeter`).

  Abbreviation(s): cm²

### Turbidity
`Turbidity`

Turbidity is the cloudiness or haziness of a fluid caused by large numbers of individual particles that are generally invisible to the naked eye, similar to smoke in air. The measurement of turbidity is a key test of water quality.

**Default Unit**: NTU

**Units**:

- NTU (`NTU`).

  Abbreviation(s): NTU

### Volume Concentration
`VolumeConcentration`

The volume concentration (not to be confused with volume fraction) is defined as the volume of a constituent divided by the total volume of the mixture.

**Default Unit**: DecimalFraction

**Units**:

- Decimal Fraction (`DecimalFraction`)
- Liter Per Liter (`LiterPerLiter`).

  Abbreviation(s): l/l
- Liter Per Milliliter (`LiterPerMilliliter`).

  Abbreviation(s): l/ml
- Percent (`Percent`).

  Abbreviation(s): %, % (v/v)
- Part Per Thousand (`PartPerThousand`).

  Abbreviation(s): ‰
- Part Per Million (`PartPerMillion`).

  Abbreviation(s): ppm
- Part Per Billion (`PartPerBillion`).

  Abbreviation(s): ppb
- Part Per Trillion (`PartPerTrillion`).

  Abbreviation(s): ppt

## Ratios & Levels

### Amplitude Ratio
`AmplitudeRatio`

The strength of a signal expressed in decibels (dB) relative to one volt RMS.

**Default Unit**: DecibelVolt

**Units**:

- Decibel Volt (`DecibelVolt`).

  Abbreviation(s): dBV
- Decibel Microvolt (`DecibelMicrovolt`).

  Abbreviation(s): dBµV
- Decibel Millivolt (`DecibelMillivolt`).

  Abbreviation(s): dBmV
- Decibel Unloaded (`DecibelUnloaded`).

  Abbreviation(s): dBu

### Level
`Level`

Level is the logarithm of the ratio of a quantity Q to a reference value of that quantity, Q₀, expressed in dimensionless units.

**Default Unit**: Decibel

**Units**:

- Decibel (`Decibel`).

  Abbreviation(s): dB
- Neper (`Neper`).

  Abbreviation(s): Np

### Ratio
`Ratio`

In mathematics, a ratio is a relationship between two numbers of the same kind (e.g., objects, persons, students, spoonfuls, units of whatever identical dimension), usually expressed as "a to b" or a:b, sometimes expressed arithmetically as a dimensionless quotient of the two that explicitly indicates how many times the first number contains the second (not necessarily an integer).

**Default Unit**: DecimalFraction

**Units**:

- Decimal Fraction (`DecimalFraction`)
- Percent (`Percent`).

  Abbreviation(s): %
- Part Per Thousand (`PartPerThousand`).

  Abbreviation(s): ‰
- Part Per Million (`PartPerMillion`).

  Abbreviation(s): ppm
- Part Per Billion (`PartPerBillion`).

  Abbreviation(s): ppb
- Part Per Trillion (`PartPerTrillion`).

  Abbreviation(s): ppt

### Ratio Change Rate
`RatioChangeRate`

The change in ratio per unit of time.

**Default Unit**: DecimalFractionPerSecond

**Units**:

- Percent Per Second (`PercentPerSecond`).

  Abbreviation(s): %/s
- Decimal Fraction Per Second (`DecimalFractionPerSecond`).

  Abbreviation(s): /s

### Relative Humidity
`RelativeHumidity`

Relative humidity is a ratio of the actual water vapor present in the air to the maximum water vapor in the air at the given temperature.

**Default Unit**: Percent

**Units**:

- Percent (`Percent`).

  Abbreviation(s): %RH

## Other

### Area Density
`AreaDensity`

The area density of a two-dimensional object is calculated as the mass per unit area. For paper this is also called grammage.

**Default Unit**: KilogramPerSquareMeter

**Units**:

- Kilogram Per Square Meter (`KilogramPerSquareMeter`).

  Abbreviation(s): kg/m²
- Gram Per Square Meter (`GramPerSquareMeter`).

  Abbreviation(s): g/m², gsm

  Also known as grammage for paper industry. In fiber industry used with abbreviation 'gsm'.
- Milligram Per Square Meter (`MilligramPerSquareMeter`).

  Abbreviation(s): mg/m²

### Area Moment Of Inertia
`AreaMomentOfInertia`

A geometric property of an area that reflects how its points are distributed with regard to an axis.

**Default Unit**: MeterToTheFourth

**Units**:

- Meter To The Fourth (`MeterToTheFourth`).

  Abbreviation(s): m⁴
- Decimeter To The Fourth (`DecimeterToTheFourth`).

  Abbreviation(s): dm⁴
- Centimeter To The Fourth (`CentimeterToTheFourth`).

  Abbreviation(s): cm⁴
- Millimeter To The Fourth (`MillimeterToTheFourth`).

  Abbreviation(s): mm⁴
- Foot To The Fourth (`FootToTheFourth`).

  Abbreviation(s): ft⁴
- Inch To The Fourth (`InchToTheFourth`).

  Abbreviation(s): in⁴

### Bit Rate
`BitRate`

In telecommunications and computing, bit rate is the number of bits that are conveyed or processed per unit of time.

**Default Unit**: BitPerSecond

**Units**:

- Bit Per Second (`BitPerSecond`).

  Abbreviation(s): bit/s, bps
- Byte Per Second (`BytePerSecond`).

  Abbreviation(s): B/s
- Octet Per Second (`OctetPerSecond`).

  Abbreviation(s): o/s

### Fluid Resistance
`FluidResistance`

Fluid Resistance is a force acting opposite to the relative motion of any object moving with respect to a surrounding fluid. Fluid Resistance is sometimes referred to as drag or fluid friction.

**Default Unit**: PascalSecondPerCubicMeter

**Units**:

- Pascal Second Per Liter (`PascalSecondPerLiter`).

  Abbreviation(s): Pa·s/l
- Pascal Minute Per Liter (`PascalMinutePerLiter`).

  Abbreviation(s): Pa·min/l
- Pascal Second Per Milliliter (`PascalSecondPerMilliliter`).

  Abbreviation(s): Pa·s/ml
- Pascal Minute Per Milliliter (`PascalMinutePerMilliliter`).

  Abbreviation(s): Pa·min/ml
- Pascal Second Per Cubic Meter (`PascalSecondPerCubicMeter`).

  Abbreviation(s): Pa·s/m³
- Pascal Minute Per Cubic Meter (`PascalMinutePerCubicMeter`).

  Abbreviation(s): Pa·min/m³
- Pascal Second Per Cubic Centimeter (`PascalSecondPerCubicCentimeter`).

  Abbreviation(s): Pa·s/cm³
- Pascal Minute Per Cubic Centimeter (`PascalMinutePerCubicCentimeter`).

  Abbreviation(s): Pa·min/cm³
- Dyne Second Per Centimeter To The Fifth (`DyneSecondPerCentimeterToTheFifth`).

  Abbreviation(s): dyn·s/cm⁵, dyn·s·cm⁻⁵
- Millimeter Mercury Second Per Liter (`MillimeterMercurySecondPerLiter`).

  Abbreviation(s): mmHg·s/l
- Millimeter Mercury Minute Per Liter (`MillimeterMercuryMinutePerLiter`).

  Abbreviation(s): mmHg·min/l
- Millimeter Mercury Second Per Milliliter (`MillimeterMercurySecondPerMilliliter`).

  Abbreviation(s): mmHg·s/ml
- Millimeter Mercury Minute Per Milliliter (`MillimeterMercuryMinutePerMilliliter`).

  Abbreviation(s): mmHg·min/ml
- Millimeter Mercury Second Per Cubic Centimeter (`MillimeterMercurySecondPerCubicCentimeter`).

  Abbreviation(s): mmHg·s/cm³
- Millimeter Mercury Minute Per Cubic Centimeter (`MillimeterMercuryMinutePerCubicCentimeter`).

  Abbreviation(s): mmHg·min/cm³
- Millimeter Mercury Second Per Cubic Meter (`MillimeterMercurySecondPerCubicMeter`).

  Abbreviation(s): mmHg·s/m³
- Millimeter Mercury Minute Per Cubic Meter (`MillimeterMercuryMinutePerCubicMeter`).

  Abbreviation(s): mmHg·min/m³
- Wood Unit (`WoodUnit`).

  Abbreviation(s): WU, HRU

### Frequency
`Frequency`

The number of occurrences of a repeating event per unit time.

**Default Unit**: Hertz

**Units**:

- Hertz (`Hertz`).

  Abbreviation(s): Hz
- Radian Per Second (`RadianPerSecond`).

  Abbreviation(s): rad/s

  In SI units, angular frequency is normally presented with the unit radian per second, and need not express a rotational value. The unit hertz (Hz) is dimensionally equivalent, but by convention it is only used for frequency f, never for angular frequency ω. This convention is used to help avoid the confusion that arises when dealing with quantities such as frequency and angular quantities because the units of measure (such as cycle or radian) are considered to be one and hence may be omitted when expressing quantities in terms of SI units.
- Cycle Per Minute (`CyclePerMinute`).

  Abbreviation(s): cpm
- Cycle Per Hour (`CyclePerHour`).

  Abbreviation(s): cph
- Beat Per Minute (`BeatPerMinute`).

  Abbreviation(s): bpm
- Per Second (`PerSecond`).

  Abbreviation(s): s⁻¹

### Lapse Rate
`LapseRate`

**Units**:


### Leak Rate
`LeakRate`

A leakage rate of QL = 1 Pa-m³/s is given when the pressure in a closed, evacuated container with a volume of 1 m³ rises by 1 Pa per second or when the pressure in the container drops by 1 Pa in the event of overpressure.

**Default Unit**: PascalCubicMeterPerSecond

**Units**:

- Pascal Cubic Meter Per Second (`PascalCubicMeterPerSecond`).

  Abbreviation(s): Pa·m³/s
- Millibar Liter Per Second (`MillibarLiterPerSecond`).

  Abbreviation(s): mbar·l/s
- Torr Liter Per Second (`TorrLiterPerSecond`).

  Abbreviation(s): Torr·l/s

### Linear Power Density
`LinearPowerDensity`

The Linear Power Density of a substance is its power per unit length.  The term linear density is most often used when describing the characteristics of one-dimensional objects, although linear density can also be used to describe the density of a three-dimensional quantity along one particular dimension.

**Default Unit**: WattPerMeter

**Units**:

- Watt Per Meter (`WattPerMeter`).

  Abbreviation(s): W/m
- Watt Per Centimeter (`WattPerCentimeter`).

  Abbreviation(s): W/cm
- Watt Per Millimeter (`WattPerMillimeter`).

  Abbreviation(s): W/mm
- Watt Per Inch (`WattPerInch`).

  Abbreviation(s): W/in
- Watt Per Foot (`WattPerFoot`).

  Abbreviation(s): W/ft

### Molar Flow
`MolarFlow`

Molar flow is the ratio of the amount of substance change to the time during which the change occurred (value of amount of substance changes per unit time).

**Default Unit**: MolePerSecond

**Units**:

- Mole Per Second (`MolePerSecond`).

  Abbreviation(s): mol/s
- Mole Per Minute (`MolePerMinute`).

  Abbreviation(s): mol/min
- Mole Per Hour (`MolePerHour`).

  Abbreviation(s): kmol/h
- Pound Mole Per Second (`PoundMolePerSecond`).

  Abbreviation(s): lbmol/s
- Pound Mole Per Minute (`PoundMolePerMinute`).

  Abbreviation(s): lbmol/min
- Pound Mole Per Hour (`PoundMolePerHour`).

  Abbreviation(s): lbmol/h

### Molar Mass
`MolarMass`

In chemistry, the molar mass M is a physical property defined as the mass of a given substance (chemical element or chemical compound) divided by the amount of substance.

**Default Unit**: KilogramPerMole

**Units**:

- Gram Per Mole (`GramPerMole`).

  Abbreviation(s): g/mol
- Kilogram Per Kilomole (`KilogramPerKilomole`).

  Abbreviation(s): kg/kmol
- Pound Per Mole (`PoundPerMole`).

  Abbreviation(s): lb/mol

### Power
`Power`

In physics, power is the rate of doing work. It is equivalent to an amount of energy consumed per unit time.

**Default Unit**: Watt

**Units**:

- Watt (`Watt`).

  Abbreviation(s): W
- Mechanical Horsepower (`MechanicalHorsepower`).

  Abbreviation(s): hp(I)

  Assuming the third CGPM (1901, CR 70) definition of standard gravity, gn = 9.80665 m/s2, is used to define the pound-force as well as the kilogram force, and the international avoirdupois pound (1959), one imperial horsepower is: 76.0402249 × 9.80665 kg⋅m2/s3
- Metric Horsepower (`MetricHorsepower`).

  Abbreviation(s): hp(M)

  DIN 66036 defines one metric horsepower as the power to raise a mass of 75 kilograms against the Earth's gravitational force over a distance of one metre in one second:[18] 75 kg × 9.80665 m/s2 × 1 m / 1 s = 75 kgf⋅m/s = 1 PS. This is equivalent to 735.49875 W, or 98.6% of an imperial horsepower.
- Electrical Horsepower (`ElectricalHorsepower`).

  Abbreviation(s): hp(E)

  Nameplates on electrical motors show their power output, not the power input (the power delivered at the shaft, not the power consumed to drive the motor). This power output is ordinarily stated in watts or kilowatts. In the United States, the power output is stated in horsepower, which for this purpose is defined as exactly 746 W.
- Boiler Horsepower (`BoilerHorsepower`).

  Abbreviation(s): hp(S)

  Boiler horsepower is a boiler's capacity to deliver steam to a steam engine and is not the same unit of power as the 550 ft lb/s definition. One boiler horsepower is equal to the thermal energy rate required to evaporate 34.5 pounds (15.6 kg) of fresh water at 212 °F (100 °C) in one hour.
- Hydraulic Horsepower (`HydraulicHorsepower`).

  Abbreviation(s): hp(H)

  Hydraulic horsepower can represent the power available within hydraulic machinery, power through the down-hole nozzle of a drilling rig, or can be used to estimate the mechanical power needed to generate a known hydraulic flow rate.
- British Thermal Unit Per Hour (`BritishThermalUnitPerHour`).

  Abbreviation(s): Btu/h, Btu/hr
- Joule Per Hour (`JoulePerHour`).

  Abbreviation(s): J/h
- Ton Of Refrigeration (`TonOfRefrigeration`).

  Abbreviation(s): TR

### Power Density
`PowerDensity`

The amount of power in a volume.

**Default Unit**: WattPerCubicMeter

**Units**:

- Watt Per Cubic Meter (`WattPerCubicMeter`).

  Abbreviation(s): W/m³
- Watt Per Cubic Inch (`WattPerCubicInch`).

  Abbreviation(s): W/in³
- Watt Per Cubic Foot (`WattPerCubicFoot`).

  Abbreviation(s): W/ft³
- Watt Per Liter (`WattPerLiter`).

  Abbreviation(s): W/l

### Power Ratio
`PowerRatio`

The strength of a signal expressed in decibels (dB) relative to one watt.

**Default Unit**: DecibelWatt

**Units**:

- Decibel Watt (`DecibelWatt`).

  Abbreviation(s): dBW
- Decibel Milliwatt (`DecibelMilliwatt`).

  Abbreviation(s): dBmW, dBm

### Reciprocal Area
`ReciprocalArea`

Reciprocal area (Inverse-square) quantity is used to specify a physical quantity inversely proportional to the square of the distance.

**Default Unit**: InverseSquareMeter

**Units**:

- Inverse Square Meter (`InverseSquareMeter`).

  Abbreviation(s): m⁻²
- Inverse Square Kilometer (`InverseSquareKilometer`).

  Abbreviation(s): km⁻²
- Inverse Square Decimeter (`InverseSquareDecimeter`).

  Abbreviation(s): dm⁻²
- Inverse Square Centimeter (`InverseSquareCentimeter`).

  Abbreviation(s): cm⁻²
- Inverse Square Millimeter (`InverseSquareMillimeter`).

  Abbreviation(s): mm⁻²
- Inverse Square Micrometer (`InverseSquareMicrometer`).

  Abbreviation(s): µm⁻²
- Inverse Square Mile (`InverseSquareMile`).

  Abbreviation(s): mi⁻²
- Inverse Square Yard (`InverseSquareYard`).

  Abbreviation(s): yd⁻²
- Inverse Square Foot (`InverseSquareFoot`).

  Abbreviation(s): ft⁻²
- Inverse Us Survey Square Foot (`InverseUsSurveySquareFoot`).

  Abbreviation(s): ft⁻² (US)
- Inverse Square Inch (`InverseSquareInch`).

  Abbreviation(s): in⁻²

### Reciprocal Length
`ReciprocalLength`

Reciprocal (Inverse) Length is used in various fields of science and mathematics. It is defined as the inverse value of a length unit.

**Default Unit**: InverseMeter

**Units**:

- Inverse Meter (`InverseMeter`).

  Abbreviation(s): m⁻¹, 1/m
- Inverse Centimeter (`InverseCentimeter`).

  Abbreviation(s): cm⁻¹, 1/cm
- Inverse Millimeter (`InverseMillimeter`).

  Abbreviation(s): mm⁻¹, 1/mm
- Inverse Mile (`InverseMile`).

  Abbreviation(s): mi⁻¹, 1/mi
- Inverse Yard (`InverseYard`).

  Abbreviation(s): yd⁻¹, 1/yd
- Inverse Foot (`InverseFoot`).

  Abbreviation(s): ft⁻¹, 1/ft
- Inverse Us Survey Foot (`InverseUsSurveyFoot`).

  Abbreviation(s): ftUS⁻¹, 1/ftUS
- Inverse Inch (`InverseInch`).

  Abbreviation(s): in⁻¹, 1/in
- Inverse Mil (`InverseMil`).

  Abbreviation(s): mil⁻¹, 1/mil
- Inverse Microinch (`InverseMicroinch`).

  Abbreviation(s): µin⁻¹, 1/µin

### Thermal Insulance
`ThermalInsulance`

Thermal insulance (R-value) is a measure of a material's resistance to the heat current. It quantifies how effectively a material can resist the transfer of heat through conduction, convection, and radiation. It has the units square metre kelvins per watt (m2⋅K/W) in SI units or square foot degree Fahrenheit–hours per British thermal unit (ft2⋅°F⋅h/Btu) in imperial units. The higher the thermal insulance, the better a material insulates against heat transfer. It is commonly used in construction to assess the insulation properties of materials such as walls, roofs, and insulation products.

**Default Unit**: SquareMeterKelvinPerKilowatt

**Units**:

- Square Meter Kelvin Per Kilowatt (`SquareMeterKelvinPerKilowatt`).

  Abbreviation(s): m²K/kW
- Square Meter Kelvin Per Watt (`SquareMeterKelvinPerWatt`).

  Abbreviation(s): m²K/W
- Square Meter Degree Celsius Per Watt (`SquareMeterDegreeCelsiusPerWatt`).

  Abbreviation(s): m²°C/W
- Square Centimeter Kelvin Per Watt (`SquareCentimeterKelvinPerWatt`).

  Abbreviation(s): cm²K/W
- Square Millimeter Kelvin Per Watt (`SquareMillimeterKelvinPerWatt`).

  Abbreviation(s): mm²K/W
- Square Centimeter Hour Degree Celsius Per Kilocalorie (`SquareCentimeterHourDegreeCelsiusPerKilocalorie`).

  Abbreviation(s): cm²Hr°C/kcal
- Hour Square Feet Degree Fahrenheit Per Btu (`HourSquareFeetDegreeFahrenheitPerBtu`).

  Abbreviation(s): Hrft²°F/Btu

### Vitamin A
`VitaminA`

Vitamin A: 1 IU is the biological equivalent of 0.3 µg retinol, or of 0.6 µg beta-carotene.

**Default Unit**: InternationalUnit

**Units**:

- International Unit (`InternationalUnit`).

  Abbreviation(s): IU

