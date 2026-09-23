Set-StrictMode -Version Latest
$ErrorActionPreference = 'Stop'
. "$PSScriptRoot/../Common.ps1"
. "$PSScriptRoot/../Sources.ps1"
. "$PSScriptRoot/../Catalog.ps1"
. "$PSScriptRoot/Test-SwitchSources.ps1"
. "$PSScriptRoot/Test-KeycapLendar.ps1"
. "$PSScriptRoot/Test-Aliases.ps1"

$script:Passed = 0
$script:Failures = @()
$testRoot = Join-Path (Split-Path (Split-Path (Split-Path $PSScriptRoot -Parent) -Parent) -Parent) ('artifacts/catalog-import/tests-' + [guid]::NewGuid().ToString('N'))
[IO.Directory]::CreateDirectory($testRoot) | Out-Null

function Assert($Condition, [string]$Message) { if (-not $Condition) { throw $Message } }
function Assert-Throws([scriptblock]$Action, [string]$Pattern) {
    try { & $Action } catch { if ($_.Exception.Message -notmatch $Pattern) { throw }; return }
    throw "Expected failure matching '$Pattern'."
}
function Test-Case([string]$Name, [scriptblock]$Action) {
    try { & $Action; $script:Passed++; Write-Host "PASS $Name" }
    catch { $script:Failures += "$Name`: $($_.Exception.Message) at $($_.ScriptStackTrace)"; Write-Host "FAIL $Name`: $($_.Exception.Message)" }
}
function Copy-Value($Value) { ConvertTo-CatalogMap (ConvertFrom-Json (ConvertTo-CatalogJson $Value)) }
function New-TestRecords {
    @(
        (Convert-CatalogProduct (Read-CatalogJson "$PSScriptRoot/Fixtures/switches.json").products[0] 'switchoddities'),
        (Convert-CatalogProduct (Read-CatalogJson "$PSScriptRoot/Fixtures/products.json").products[0] 'divinikey'),
        (Convert-CatalogMatrix ([IO.File]::ReadAllText("$PSScriptRoot/Fixtures/matrix.md")) @{ sourcePath = 'docs/gmk-keycaps/Example-R2.md' } ('a' * 40))
    )
}
function New-TestState { (New-CatalogCandidate (Read-CatalogState (Join-Path $testRoot 'empty')) (New-TestRecords)).state }
function Save-TestFetch([string]$Run) {
    $manifest = [ordered]@{ schemaVersion = 1; fetchedAt = '2026-09-09T00:00:00Z'; sources = [ordered]@{} }
    foreach ($source in $script:CatalogSources.Keys) {
        if ($source -eq 'dcswiki') {
            $files = @()
            foreach ($extension in @('html', 'js')) {
                $relative = "raw/dcswiki/catalog.$extension"
                $path = Join-Path $Run $relative
                [IO.Directory]::CreateDirectory((Split-Path $path -Parent)) | Out-Null
                [IO.File]::Copy("$PSScriptRoot/Fixtures/dcswiki.$extension", $path)
                $role = if ($extension -eq 'html') { 'page' } else { 'bundle' }
                $url = if ($extension -eq 'html') { 'https://dcs.wiki/keycaps' } else { 'https://dcs.wiki/_next/static/chunks/123-changing-hash.js' }
                $files += [ordered]@{ path = $relative; sha256 = Get-CatalogHash $path; url = $url; role = $role }
            }
            $manifest.sources[$source] = [ordered]@{ fetchedAt = '2026-09-09T01:00:00Z'; files = $files }
        } elseif ($source -eq 'keycaplendar') {
            $relative = 'raw/keycaplendar/page-1.json'
            $path = Join-Path $Run $relative
            Write-CatalogJson $path (Read-CatalogJson "$PSScriptRoot/Fixtures/keycaplendar.json")
            $manifest.sources[$source] = [ordered]@{ files = @([ordered]@{ path = $relative; sha256 = Get-CatalogHash $path; url = Get-KeycapLendarPageUrl }) }
        } elseif ($source -eq 'theremingoat') {
            $relative = 'raw/theremingoat/collection.xlsx'
            $path = Join-Path $Run $relative
            $items = New-TestCollectionItems
            $items[2].type = 'Clicky'
            Save-TestCollection $path $items
            $manifest.sources[$source] = [ordered]@{ files = @([ordered]@{ path = $relative; sha256 = Get-CatalogHash $path; url = $script:CatalogSources.theremingoat.endpoint }) }
        } elseif ($source -eq 'theremingoatscores') {
            $relative = 'raw/theremingoatscores/scores.csv'
            $path = Join-Path $Run $relative
            Save-TestScoreSheet $path ((New-TestScoreSheetText).Replace(',Hall Effect,', ',Linear,'))
            $manifest.sources[$source] = [ordered]@{ files = @([ordered]@{ path = $relative; sha256 = Get-CatalogHash $path; url = $script:CatalogSources.theremingoatscores.endpoint }) }
        } elseif ($source -eq 'matrix') {
            $relative = 'raw/matrix/Example-R2.md'
            $path = Join-Path $Run $relative
            [IO.Directory]::CreateDirectory((Split-Path $path -Parent)) | Out-Null
            [IO.File]::Copy("$PSScriptRoot/Fixtures/matrix.md", $path)
            $manifest.sources[$source] = [ordered]@{ commit = ('a' * 40); files = @([ordered]@{ path = $relative; sha256 = Get-CatalogHash $path; sourcePath = 'docs/gmk-keycaps/Example-R2.md' }) }
        } else {
            $fixture = if ($source -eq 'switchoddities') { 'switches' } else { 'products' }
            $data = if ($source -eq 'unikeys') { [ordered]@{ products = @((New-TestUniKeysProduct)) } } else { Read-CatalogJson "$PSScriptRoot/Fixtures/$fixture.json" }
            if ($source -eq 'kbdfans') {
                $data.products[0].title = 'Different Set'; $data.products[0].id = 301
                $data.products[1].title = 'Different Novelties'; $data.products[1].id = 302
            } elseif ($source -notin @('switchoddities', 'unikeys', 'divinikey')) {
                # Every other retailer shares the Divinikey fixture, so each needs its own
                # names or the shared products would read as cross-source duplicates.
                $offset = 400 + 10 * @($script:CatalogSources.Keys).IndexOf($source)
                $data.products[0].title = "$source Set"; $data.products[0].id = $offset + 1
                $data.products[1].title = "$source Novelties"; $data.products[1].id = $offset + 2
                if ($script:CatalogSources[$source].kind -eq 'switches') {
                    # The shared fixture's "Kit" option is a keycap concept a switch source never sees.
                    $data.products[0].variants = @($data.products[0].variants[0]); $data.products[0].options = @()
                    foreach ($product in $data.products) { $product.body_html = '<p>Switch Type: Linear</p>' }
                }
            }
            $files = @()
            foreach ($page in 1..2) {
                $relative = "raw/$source/page-$page.json"
                $path = Join-Path $Run $relative
                $content = if ($page -eq 1) { $data } else { [ordered]@{ products = @() } }
                Write-CatalogJson $path $content
                $files += [ordered]@{ path = $relative; sha256 = Get-CatalogHash $path }
            }
            $manifest.sources[$source] = [ordered]@{ files = $files }
        }
    }
    Write-CatalogJson (Join-Path $Run 'fetch.json') $manifest
}

Test-SwitchSources
Test-KeycapLendar
Test-CatalogAliases

Test-Case 'DCS Wiki discovers changing bundles and decodes data without running JavaScript' {
    $html = [IO.File]::ReadAllText("$PSScriptRoot/Fixtures/dcswiki.html")
    $bundle = [IO.File]::ReadAllText("$PSScriptRoot/Fixtures/dcswiki.js")
    $urls = Get-DcsWikiBundleUrls ($html.Replace('123-changing-hash', '456-new-hash'))
    Assert ($urls.Count -eq 1 -and $urls[0].EndsWith('/456-new-hash.js')) 'Bundle discovery was hardcoded or allowed an external script.'
    $script:DcsFixtureExecuted = $false
    $items = Get-DcsWikiData ($bundle + '; $script:DcsFixtureExecuted = $true;')
    Assert (-not $script:DcsFixtureExecuted) 'Downloaded script was executed.'
    $records = @(Convert-DcsWikiRecords $items $html)
    Assert ($records.Count -eq 3 -and $records[0].entry.name -ceq 'DCS Explorer''s "R2"') 'JS/JSON quote escapes were not decoded.'
    Assert ($records[0].entry.designer -ceq 'Alice & Bob' -and $records[0].entry.profile -eq 'DCS') 'Unicode escape or profile lost; legend style is not profile.'
    Assert (-not $records[0].entry.Contains('description') -and -not $records[0].entry.Contains('imageUrl')) 'Unsupported source fields imported.'
    Assert (-not $records[1].excluded -and -not $records[2].excluded) 'Add-on kit or complete monokit was excluded.'
    Assert (-not $records[2].entry.Contains('designer') -and -not $records[2].entry.Contains('manufacturer')) 'Unknown metadata was retained.'
    Assert ((ConvertFrom-CatalogJsString "'\x41\u0042\\'") -ceq 'AB\') 'Hex/backslash escape failed.'
}
Test-Case 'DCS Wiki rejects changed schemas, ambiguous arrays, missing links, and incomplete caches' {
    $html = [IO.File]::ReadAllText("$PSScriptRoot/Fixtures/dcswiki.html")
    $bundle = [IO.File]::ReadAllText("$PSScriptRoot/Fixtures/dcswiki.js")
    Assert-Throws { Get-DcsWikiData ($bundle.Replace('"id":"502"', '"id":"501"')) } 'duplicate DCS Wiki ID'
    Assert-Throws { Get-DcsWikiData ($bundle.Replace('"name":', '"renamedField":')) } 'missing valid name'
    Assert-Throws { Get-DcsWikiData ($bundle + $bundle) } 'Ambiguous'
    Assert-Throws { ConvertFrom-CatalogJsString "'\xZZ'" } 'hex escape'
    Assert-Throws { ConvertFrom-CatalogJsString "'\q'" } 'Unsupported'
    Assert-Throws { Convert-DcsWikiRecords (Get-DcsWikiData $bundle) '' } 'page/bundle mismatch'
    $run = Join-Path $testRoot 'dcs-cache'
    Save-TestFetch $run
    $manifest = Read-CatalogJson (Join-Path $run 'fetch.json')
    $files = $manifest.sources.dcswiki.files
    Assert-Throws { Read-DcsWikiFetch $run @($files[0]) } 'Incomplete'
    $files[1].url = 'https://dcs.wiki/_next/static/chunks/stale.js'
    Assert-Throws { Read-DcsWikiFetch $run $files } 'not referenced'
    $manifest.sources.Remove('dcswiki')
    Write-CatalogJson (Join-Path $run 'fetch.json') $manifest
    Assert-Throws { Read-CatalogFetch $run } 'incomplete fetch manifest'
}
Test-Case 'DCS Wiki fetch caches the discovered data and fails when it disappears' {
    function Get-CatalogResponse([string]$Url) {
        if ($Url -eq 'https://dcs.wiki/keycaps') { return [IO.File]::ReadAllText("$PSScriptRoot/Fixtures/dcswiki.html") }
        if ($Url -eq 'https://dcs.wiki/_next/static/chunks/123-changing-hash.js') { return [IO.File]::ReadAllText("$PSScriptRoot/Fixtures/dcswiki.js") }
        throw 'Unexpected request'
    }
    $run = Join-Path $testRoot 'dcs-fetch'
    $snapshot = Save-DcsWikiFetch $run
    Assert ($snapshot.files.Count -eq 2 -and (Read-DcsWikiFetch $run $snapshot.files).Count -eq 3) 'Fetch could not be replayed offline.'
    [IO.File]::AppendAllText((Join-Path $run 'raw/dcswiki/catalog.js'), ' ')
    Assert-Throws { Read-DcsWikiFetch $run $snapshot.files } 'checksum mismatch'
    function Get-CatalogResponse([string]$Url) {
        if ($Url -eq 'https://dcs.wiki/keycaps') { return [IO.File]::ReadAllText("$PSScriptRoot/Fixtures/dcswiki.html") }
        return 'JSON.parse(''[]'')'
    }
    Assert-Throws { Save-DcsWikiFetch (Join-Path $testRoot 'dcs-empty') } 'catalog data not found'
}

Test-Case 'Product labels, HTML entities, standalone kits, and retailer attribution' {
    $products = (Read-CatalogJson "$PSScriptRoot/Fixtures/products.json").products
    $p = Convert-CatalogProduct $products[0] 'divinikey'
    Assert ($p.entry.name -ceq 'Example & Friends') 'Name normalization failed.'
    Assert ($p.entry.manufacturer -eq 'Example Factory' -and $p.entry.brand -eq 'Example Brand') 'Manufacturer and brand were conflated.'
    Assert ($p.entry.designer -eq 'Alice & Bob' -and $p.entry.material -eq 'ABS' -and $p.entry.profile -eq 'Cherry') 'Explicit metadata lost.'
    Assert (-not (Convert-CatalogProduct $products[1] 'divinikey').excluded) 'Standalone novelty kit was excluded.'
    $s = (New-TestRecords)[0]
    Assert (-not $s.entry.Contains('manufacturer') -and -not $s.entry.Contains('brand') -and $s.entry.switchType -eq 'linear') 'Retailer/prose invented switch metadata.'
    $table = Get-CatalogText '<table><tr><td><p>Manufacturer</p></td><td><p>JWK</p></td></tr><tr><td>Designer</td><td>Durock</td></tr></table>'
    Assert ((Get-CatalogLabel $table 'Manufacturer(?:[ \t]*:|[ \t]+)') -eq 'JWK') 'Table-cell layout lost metadata.'
}
Test-Case 'Keycap add-ons are retained across sources while references, accessories, and artisans remain excluded' {
    $product = (Read-CatalogJson "$PSScriptRoot/Fixtures/products.json").products[1]
    foreach ($source in @('divinikey', 'kbdfans')) {
        foreach ($name in @('Example Spacebars', 'Example Accent Kit', 'Example International Add-on', 'Example Relegendables', 'Example Metal Keycaps Set')) {
            $product.title = $name
            $record = Convert-CatalogProduct $product $source
            $expected = $name -replace ' Keycaps Set$', ''
            Assert (-not $record.excluded -and $record.entry.name -ceq $expected) "Keycap add-on lost: $source/$name"
        }
        $product.title = 'Example Keycap Puller'
        Assert ((Convert-CatalogProduct $product $source).excluded) 'Non-keycap accessory was accepted.'
    }
    foreach ($listing in @(
        @{ title = 'Example Artisan'; vendor = 'Example' }
        @{ title = 'Example Artisans'; vendor = 'Example' }
        @{ title = 'Example Machined Keycap'; vendor = 'Example' }
        @{ title = 'Example Brass Keycap'; vendor = 'Example' }
        @{ title = 'Example + Salvun keycap'; vendor = 'Example' }
        @{ title = 'Example Orenji'; vendor = 'Salvun' }
        @{ title = 'Example Artisan Extras'; vendor = 'Artisan' }
    )) {
        $product = [ordered]@{ id = 962; title = $listing.title; handle = 'x'; vendor = $listing.vendor; body_html = ''; options = @(); variants = @() }
        Assert ((Convert-CatalogProduct $product 'omnitype').excluded -match 'Artisan') "$($listing.title) was accepted as a keycap set."
    }
    # A studio that also sells full sets is not an artisan maker.
    $set = [ordered]@{ id = 963; title = 'PBT Office Beige'; handle = 'x'; vendor = 'HIBI'; body_html = ''; options = @(); variants = @() }
    Assert (-not (Convert-CatalogProduct $set 'omnitype').excluded) 'A keycap set from an artisan studio was rejected.'
    foreach ($name in @('Example 40s Addon', 'Example Modifiers', 'Example Accent', 'Example Spacebars', 'Example Relegendables')) {
        $record = Convert-CatalogMatrix "title: $name" @{ sourcePath = 'docs/gmk-keycaps/Example.md' } ('a' * 40)
        Assert (-not $record.excluded -and $record.entry.name -ceq "GMK $name") "Matrix add-on lost: $name"
    }
    foreach ($name in @('GMK Keycaps', 'Standard Color Codes')) {
        Assert ((Convert-CatalogMatrix "title: $name" @{ sourcePath = 'docs/gmk-keycaps/reference.md' } ('a' * 40)).excluded) 'Reference page was accepted.'
    }
}
Test-Case 'Titles full of specifications yield the set name, its shape, and its plastic' {
    function New-Listing([string]$Title, [string]$Type = '', [string]$Body = '') {
        [ordered]@{ id = 970; title = $Title; handle = 'x'; vendor = 'Example'; product_type = $Type; body_html = $Body; options = @(); variants = @() }
    }
    $record = Convert-CatalogProduct (New-Listing 'Tai-Hao Midnight Sun 114 Key Cubic Double Shot ABS Keycap Set' 'Cubic Profile Keycaps') 'mechanicalkeyboards'
    Assert ($record.entry.name -ceq 'Tai-Hao Midnight Sun' -and $record.entry.profile -ceq 'Cubic' -and $record.entry.material -ceq 'ABS') 'Title specifications were not separated.'
    $record = Convert-CatalogProduct (New-Listing 'Signature Plastics SA Solarized 152 Key SA Profile Double Shot ABS Keycap Set') 'mechanicalkeyboards'
    Assert ($record.entry.name -ceq 'Signature Plastics SA Solarized' -and $record.entry.profile -ceq 'SA') 'A shape word inside the name was removed.'
    $record = Convert-CatalogProduct (New-Listing 'Glorious PC GPBT Pastel 114 Key Cherry Profile Dye Sub PBT Keycap Set') 'mechanicalkeyboards'
    Assert ($record.entry.name -ceq 'Glorious PC GPBT Pastel' -and $record.entry.material -ceq 'PBT') 'PC in a company name was read as a plastic.'
    Assert ((Convert-CatalogProduct (New-Listing 'Chilkey Wild Rose 170 Key DDA Profile Dye Sub PC Keycap Set') 'mechanicalkeyboards').entry.material -ceq 'PC') 'PC beside other specifications was not read.'
    Assert ((Convert-CatalogProduct (New-Listing 'GMK Arctic Base Kit Cherry Profile Double Shot ABS Keycap Set') 'mechanicalkeyboards').entry.name -ceq 'GMK Arctic') 'A base kit was not read as its set.'
    $record = Convert-CatalogProduct (New-Listing 'KBDFans PBTfans Twist 40s Kit 36 Key Cherry Profile Double Shot PBT Add-on Keycap Set') 'mechanicalkeyboards'
    Assert ($record.entry.name -ceq 'PBTfans Twist 40s Kit' -and -not $record.excluded) 'An add-on kit was lost.'
    $record = Convert-CatalogProduct (New-Listing 'ISO Cherry Profile Dye-Sub PBT Full Set Keycap Set - Developer') 'keychron'
    Assert ($record.entry.name -ceq 'Keychron Developer ISO' -and $record.entry.brand -ceq 'Keychron') 'A single-brand store listing lost its brand or layout.'
    $record = Convert-CatalogProduct (New-Listing 'Quantum Horizon Keycap Set (126-Key)') 'akkogear'
    Assert ($record.entry.name -ceq 'Akko Quantum Horizon' -and -not $record.excluded) 'A bracketed key count stayed in the name.'
    foreach ($case in @(
        @{ title = 'Traitors Sakura Kuro 6 Key OEM Profile Dye Sub Keycap Set'; reason = 'Individual keys' }
        @{ title = 'Leopold Red Escape Cherry Profile Dye Sub PBT Keycap'; reason = 'Individual keys' }
        @{ title = 'Ji Zun Blue Camo OEM Profile Dye Sub PBT Spacebar'; reason = 'Individual keys' }
        @{ title = 'Tai-Hao Neon Blue Backlit 22 Key OEM Profile Double Shot ABS TPR Keycap Set'; reason = 'Rubber' }
        @{ title = 'Double Shot KSA PBT Keycap Full Keycap Set'; reason = 'generic' }
        @{ title = 'Dwarf Factory Happy Hippo Kaba'; type = 'Artisan Keycaps'; reason = 'Artisan' }
    )) {
        $type = if ($case.Contains('type')) { $case.type } else { '' }
        Assert ((Convert-CatalogProduct (New-Listing $case.title $type) 'mechanicalkeyboards').excluded -match $case.reason) "$($case.title) was accepted as a keycap set."
    }
    $record = Convert-CatalogProduct (New-Listing 'GMK Example Cherry Profile Double Shot ABS Keycap Set' '' 'Designed by Wynects and inspired by manta rays') 'mechanicalkeyboards'
    Assert ($record.entry.designer -ceq 'Wynects') 'Store prose stayed in the designer credit.'
}
Test-Case 'Unicode names and meaningful rounds survive parsing' {
    $p = (New-TestRecords)[2]
    $expected = 'GMK Example R2 ' + [char]0x793A + [char]0x4F8B
    Assert ($p.entry.name -ceq $expected -and $p.entry.designer -eq 'Example Studio') 'Matrix fields lost.'
    Assert ((Get-CatalogMatchName 'GMK Example R2') -ne (Get-CatalogMatchName 'GMK Example R1')) 'Rounds collapsed.'
    Assert ((Get-CatalogMatchName 'GMK Example R2') -eq (Get-CatalogMatchName $p.entry.name)) 'Translation matching failed.'
    Assert ((Get-CatalogSlug 'GMK Olivia++') -ne (Get-CatalogSlug 'GMK Olivia')) 'Meaningful plus signs collapsed.'
    Assert (-not (Get-CatalogLabel "* Designer:  `n* GB Time: 2020-01" 'Designer\s*:')) 'Blank label consumed the next line.'
}
Test-Case 'Pagination rejects repeated IDs and malformed responses' {
    $data = Read-CatalogJson "$PSScriptRoot/Fixtures/products.json"
    $seen = @{}
    Test-CatalogProductPage $data $seen
    Assert-Throws { Test-CatalogProductPage $data $seen } 'Repeated product'
    Assert-Throws { Test-CatalogProductPage @{ products = 'wrong' } @{} } 'products array'
    Assert-Throws { Test-CatalogProductPage @{ products = @(@{ id = 1; title = '' }) } @{} } 'missing its ID or name'
}
Test-Case 'Replay requires a complete checksummed fetch and expands switch weights' {
    $run = Join-Path $testRoot 'fetch'
    Save-TestFetch $run
    $records = Read-CatalogFetch $run
    $switches = @($records | Where-Object { $_.source -eq 'switchoddities' })
    Assert ($switches.Count -eq 2 -and $switches[0].sourceId -ne $switches[1].sourceId) 'Weights were collapsed.'
    Assert ($switches[0].entry.name -match '38g' -and $switches[1].entry.name -match '53g') 'Weight names lost.'
    [IO.File]::AppendAllText((Join-Path $run 'raw/switchoddities/page-1.json'), ' ')
    Assert-Throws { Read-CatalogFetch $run } 'checksum mismatch'
    Assert-Throws { Read-CatalogFetch (Join-Path $testRoot 'missing') } 'fetch.json'
}
Test-Case 'Stores that write a generic Switch(es) word clean titles the same way SwitchOddities already writes them' {
    function New-Switch([string]$Title) {
        [ordered]@{ id = 971; title = $Title; handle = 'x'; vendor = 'Example'; body_html = ''; options = @(); variants = @() }
    }
    Assert ((Convert-CatalogProduct (New-Switch 'AEBoards Blaeck Linear Switch (40)') 'gateron').entry.name -ceq 'AEBoards Blaeck Linear') 'A bare pack count in parentheses was not dropped.'
    Assert ((Convert-CatalogProduct (New-Switch 'Akko Bittersweet Switch (Tactile, 45pcs)') 'gateron').entry.name -ceq 'Akko Bittersweet Tactile') 'A type paired with a pack size lost the type or kept the word Switch.'
    Assert ((Convert-CatalogProduct (New-Switch 'Anubis Tactile Switch by Mechs on Deck') 'gateron').entry.name -ceq 'Anubis Tactile') 'A trailing designer credit or the word Switch survived.'
    Assert ((Convert-CatalogProduct (New-Switch 'AEBoards Naevy Tactile Switch R1.5') 'gateron').entry.name -ceq 'AEBoards Naevy Tactile R1.5') 'The word Switch ahead of a revision was not dropped.'
    Assert ((Convert-CatalogProduct (New-Switch 'Gateron X Linear Switch') 'switchoddities').entry.name -ceq 'Gateron X Linear Switch') 'A source without the switchSuffix rule had its own wording changed.'
}
Test-Case 'A pack-size axis collapses while a real distinguishing axis composes into the name' {
    $run = Join-Path $testRoot 'switch-suffix-fetch'
    Save-TestFetch $run
    function Set-TestSwitchPage([string]$Run, [string]$Source, $Products) {
        $relative = "raw/$Source/page-1.json"
        $path = Join-Path $Run $relative
        Write-CatalogJson $path ([ordered]@{ products = $Products })
        $manifest = Read-CatalogJson (Join-Path $Run 'fetch.json')
        $manifest.sources[$Source].files[0].sha256 = Get-CatalogHash $path
        Write-CatalogJson (Join-Path $Run 'fetch.json') $manifest
    }
    $products = @(
        [ordered]@{
            id = 981; title = 'Aurora Fog Switch'; handle = 'aurora-fog'; vendor = 'Gateron'; body_html = ''
            options = @([ordered]@{ name = 'Quantity'; position = 1; values = @('36', '70') })
            variants = @([ordered]@{ id = 9811; title = '36'; option1 = '36' }, [ordered]@{ id = 9812; title = '70'; option1 = '70' })
        },
        [ordered]@{
            id = 982; title = 'Version Test Switch'; handle = 'version-test'; vendor = 'Gateron'; body_html = ''
            options = @([ordered]@{ name = 'Version'; position = 1; values = @('Alpha', 'Beta') })
            variants = @([ordered]@{ id = 9821; title = 'Alpha'; option1 = 'Alpha' }, [ordered]@{ id = 9822; title = 'Beta'; option1 = 'Beta' })
        },
        [ordered]@{
            id = 983; title = 'Lube Test Switch'; handle = 'lube-test'; vendor = 'Gateron'; body_html = ''
            options = @([ordered]@{ name = 'Factory Lube'; position = 1; values = @('No Lube', 'Hand Lubed') })
            variants = @([ordered]@{ id = 9831; title = 'No Lube'; option1 = 'No Lube' }, [ordered]@{ id = 9832; title = 'Hand Lubed'; option1 = 'Hand Lubed' })
        },
        [ordered]@{
            id = 984; title = 'Redundant Test Tactile Switch'; handle = 'redundant-test'; vendor = 'Gateron'; body_html = ''
            options = @([ordered]@{ name = 'Type'; position = 1; values = @('Tactile') }, [ordered]@{ name = 'Warehouse'; position = 2; values = @('CN', 'DE') })
            variants = @([ordered]@{ id = 9841; title = 'Tactile / CN'; option1 = 'Tactile'; option2 = 'CN' }, [ordered]@{ id = 9842; title = 'Tactile / DE'; option1 = 'Tactile'; option2 = 'DE' })
        }
    )
    Set-TestSwitchPage $run 'gateron' $products
    $allRecords = Read-CatalogFetch $run
    $records = @($allRecords | Where-Object { $_.source -eq 'gateron' })
    Assert ($records.Count -eq 6) 'Expected one pack-size product, two version variants, two lube variants (one excluded), and one redundant-axis product.'
    $aurora = @($records | Where-Object { $_.sourceId -match '^981' })
    Assert ($aurora.Count -eq 1 -and $aurora[0].entry.name -ceq 'Aurora Fog') 'A pack-size-only axis was not collapsed to one record.'
    $version = @($records | Where-Object { $_.sourceId -match '^982' })
    $alpha = @($version | Where-Object { $_.entry.name -ceq 'Version Test - Alpha' })
    $beta = @($version | Where-Object { $_.entry.name -ceq 'Version Test - Beta' })
    Assert ($version.Count -eq 2 -and $alpha.Count -eq 1 -and $beta.Count -eq 1) 'A real distinguishing axis did not compose into the name.'
    $lube = @($records | Where-Object { $_.sourceId -match '^983' })
    $lubeKept = @($lube | Where-Object { -not $_.excluded })
    $lubeExcluded = @($lube | Where-Object { $_.excluded })
    Assert ($lubeKept.Count -eq 1 -and $lubeKept[0].entry.name -ceq 'Lube Test') 'A factory lube option was not collapsed and stripped from the name.'
    Assert ($lubeExcluded.Count -eq 1 -and $lubeExcluded[0].excluded -match 'modification') 'A hand-lubed option was not excluded as an aftermarket modification.'
    $redundant = @($records | Where-Object { $_.sourceId -match '^984' })
    Assert ($redundant.Count -eq 1 -and $redundant[0].entry.name -ceq 'Redundant Test Tactile') 'A value already present in the base name was appended a second time.'
}
Test-Case 'No-op refresh is byte stable and does not bump versions' {
    $state = New-TestState
    $next = New-CatalogCandidate $state (New-TestRecords)
    Assert ((ConvertTo-CatalogJson $state) -ceq (ConvertTo-CatalogJson $next.state)) 'No-op changed state.'
    Assert ($next.report.changes.Count -eq 0 -and $next.report.additions.Count -eq 0) 'No-op reported changes.'
}
Test-Case 'Renames retain IDs, overrides persist, and missing sources retain history' {
    $state = New-TestState
    $records = New-TestRecords
    $records[0].entry.name = 'Corrected Switch Name'
    $next = New-CatalogCandidate $state $records
    Assert ($next.state.catalogs.switches.entries[0].id -eq $state.catalogs.switches.entries[0].id) 'Rename changed ID.'
    Assert ($next.state.catalogs.switches.catalogVersion -eq 2 -and $next.state.catalogs.keycaps.catalogVersion -eq 1) 'Wrong catalogs versioned.'
    $id = $state.catalogs.switches.entries[0].id
    $state.overrides.entries["switches/$id"] = [ordered]@{ name = 'Reviewed Name'; manufacturer = 'Verified Factory' }
    $next = New-CatalogCandidate $state $records
    Assert ($next.state.catalogs.switches.entries[0].name -eq 'Reviewed Name') 'Override lost.'
    $missing = New-CatalogCandidate $next.state @($records | Where-Object { $_.source -ne 'switchoddities' })
    Assert ($missing.state.catalogs.switches.entries[0].manufacturer -eq 'Verified Factory' -and $missing.report.missingSources.Count -eq 1) 'Retired entry lost.'
}
Test-Case 'Reviewed ID changes preserve absent and filtered source history' {
    foreach ($filtered in @($false, $true)) {
        $state = New-TestState
        $binding = @($state.bindings | Where-Object { $_.kind -eq 'switches' })[0]
        $sourceKey = "$($binding.source):$($binding.sourceId)"
        $oldId = $binding.id
        $state.catalogs.switches.entries[0].manufacturer = 'Reviewed Factory'
        $binding.notes = 'Historical variant detail'
        $originalBinding = ConvertTo-CatalogJson $binding
        $state.overrides.bindings[$sourceKey] = 'clean-switch'
        $state.overrides.entries['switches/clean-switch'] = [ordered]@{ name = 'Clean Switch' }
        $records = @(New-TestRecords)
        if ($filtered) { $records[0].excluded = 'New automatic filter' }
        else { $records = @($records | Where-Object { $_.kind -ne 'switches' }) }
        $next = New-CatalogCandidate $state $records
        $entry = $next.state.catalogs.switches.entries[0]
        $updated = @($next.state.bindings | Where-Object { $_.kind -eq 'switches' })[0]
        Assert ($entry.id -ceq 'clean-switch' -and $entry.name -ceq 'Clean Switch' -and $entry.manufacturer -ceq 'Reviewed Factory') 'Historical ID change lost the name or reviewed metadata.'
        Assert ($updated.id -ceq 'clean-switch' -and $updated.url -ceq $binding.url -and $updated.notes -ceq $binding.notes) 'Historical provenance lost during redirect.'
        Assert ((ConvertTo-CatalogJson $updated.observed) -ceq (ConvertTo-CatalogJson $binding.observed)) 'Historical observation was rewritten.'
        Assert ((ConvertTo-CatalogJson $binding) -ceq $originalBinding) 'Redirect mutated the input state.'
        Assert ($next.report.removals.Count -eq 1 -and $next.report.removals[0].entry -ceq "switches/$oldId") 'Old ID was not retired.'
        $again = New-CatalogCandidate $next.state $records
        Assert ((ConvertTo-CatalogJson $again.state) -ceq (ConvertTo-CatalogJson $next.state)) 'Repeated historical redirect was not stable.'
    }
}
Test-Case 'A historical redirect keeps an old entry while another source still references it' {
    $state = New-TestState
    $binding = @($state.bindings | Where-Object { $_.kind -eq 'switches' })[0]
    $second = Copy-Value $binding
    $second.sourceId = 'historical-second'
    $state.bindings += $second
    $state.overrides.bindings["$($binding.source):$($binding.sourceId)"] = 'clean-switch'
    $state.overrides.entries['switches/clean-switch'] = [ordered]@{ name = 'Clean Switch' }
    $next = New-CatalogCandidate $state @((New-TestRecords) | Where-Object { $_.kind -ne 'switches' })
    Assert ($next.state.catalogs.switches.entries.Count -eq 2 -and $next.report.removals.Count -eq 0) 'A still-referenced variant was retired.'
    Assert (@($next.state.bindings | Where-Object { $_.id -ceq $binding.id }).Count -eq 1) 'Unreviewed source was redirected.'
}
Test-Case 'Cross-source matches need explicit decisions and metadata conflicts block' {
    $state = New-TestState
    $records = @(New-TestRecords)
    $extra = Copy-Value $records[1]
    $extra.source = 'kbdfans'; $extra.sourceId = '501'
    $records += $extra
    $next = New-CatalogCandidate $state $records
    Assert ($next.report.duplicates.Count -eq 1) 'Duplicate auto-merged.'
    $id = @($state.bindings | Where-Object { $_.source -eq 'divinikey' })[0].id
    $state.overrides.bindings['kbdfans:501'] = $id
    $extra.entry.material = 'PBT'
    $next = New-CatalogCandidate $state $records
    Assert ($next.report.duplicates.Count -eq 0 -and $next.report.conflicts.Count -eq 1) 'Metadata conflict not reported.'
    $state.overrides.entries["keycaps/$id"] = [ordered]@{ material = 'ABS' }
    $next = New-CatalogCandidate $state $records
    Assert ($next.report.conflicts.Count -eq 0) 'Reviewed conflict did not resolve.'
}
Test-Case 'Validation rejects duplicate IDs, unsupported fields, and orphan mappings' {
    $state = New-TestState
    $state.catalogs.switches.entries += $state.catalogs.switches.entries[0]
    Assert-Throws { Test-CatalogState $state } 'Duplicate ID'
    $state = New-TestState
    $state.catalogs.switches.entries[0].material = 'POM'
    Assert-Throws { Test-CatalogState $state } 'Unsupported field'
    $state = New-TestState
    $state.bindings[0].id = 'missing'
    Assert-Throws { Test-CatalogState $state } 'missing ID'
}
Test-Case 'Switch types are required after reviewed overrides are applied' {
    $state = New-TestState
    $entry = $state.catalogs.switches.entries[0]
    $entry.Remove('switchType')
    Assert-Throws { Test-CatalogState $state } 'Missing switchType'
    foreach ($value in @($null, '', 'unknown')) {
        $entry.switchType = $value
        Assert-Throws { Test-CatalogState $state } 'Invalid switchType|Invalid switch type'
    }
    $records = New-TestRecords
    $switch = $records | Where-Object { $_.kind -eq 'switches' } | Select-Object -First 1
    $switch.entry.Remove('switchType')
    $empty = Read-CatalogState (Join-Path $testRoot 'empty')
    Assert-Throws { New-CatalogCandidate $empty $records } 'Missing switchType'
    $empty.overrides.entries["switches/$(Get-CatalogSlug $switch.entry.name)"] = [ordered]@{ switchType = 'linear' }
    $next = (New-CatalogCandidate $empty $records).state
    Test-CatalogState $next
}
Test-Case 'Reviewed latching and combined types survive replay and validation' {
    foreach ($type in @('latching', 'linear/clicky')) {
        $state = New-TestState
        $id = $state.catalogs.switches.entries[0].id
        $state.overrides.entries["switches/$id"] = [ordered]@{ switchType = $type }
        $next = (New-CatalogCandidate $state (New-TestRecords)).state
        Test-CatalogState $next
        Assert (($next.catalogs.switches.entries | Where-Object { $_.id -eq $id }).switchType -ceq $type) 'Reviewed type was lost on replay.'
    }
    $state = New-TestState
    $state.catalogs.switches.entries[0].switchType = 'unknown'
    Assert-Throws { Test-CatalogState $state } 'Invalid switch type'
}
Test-Case 'Promotion checks candidates, baselines, overrides, and complete state' {
    $root = Join-Path $testRoot 'promotion-root'
    $run = Join-Path $testRoot 'promotion-run'
    Save-TestFetch $run
    Save-CatalogCandidate $root $run
    Publish-CatalogCandidate $root $run
    Test-CatalogState (Read-CatalogState $root)
    $provenance = Read-CatalogJson (Join-Path $root 'Scripts/Catalogs/sources.json')
    Assert ($provenance.sourceFetchDates.dcswiki -eq '2026-09-09T01:00:00Z' -and $provenance.sourceFetchDates.matrix -eq '2026-09-09T00:00:00Z') 'Individual source retrieval dates were lost.'
    Assert-Throws { Publish-CatalogCandidate $root $run } 'Accepted files changed'
    Save-CatalogCandidate $root $run
    $file = Join-Path $run 'candidate/Assets/Catalogs/switches.json'
    [IO.File]::AppendAllText($file, ' ')
    Assert-Throws { Publish-CatalogCandidate $root $run } 'Candidate checksum mismatch'
    Save-CatalogCandidate $root $run
    Write-CatalogJson (Join-Path $root 'Scripts/Catalogs/overrides.json') (Read-CatalogState $root).overrides
    Assert-Throws { Publish-CatalogCandidate $root $run } 'inputs or report changed'
}
Test-Case 'Traversal paths are rejected' {
    Assert-Throws { Get-CatalogRelativePath $testRoot '../outside.json' } 'leaves its root'
}

Test-Case 'Failed network fetch leaves no complete manifest' {
    function Get-CatalogResponse([string]$Url) {
        if ($Url -match 'page=1$') { return [IO.File]::ReadAllText("$PSScriptRoot/Fixtures/switches.json") }
        throw 'Simulated network failure'
    }
    $run = Join-Path $testRoot 'failed-fetch'
    Assert-Throws { Save-CatalogFetch $run } 'Simulated network failure'
    Assert (-not (Test-Path -LiteralPath (Join-Path $run 'fetch.json'))) 'Failed fetch published a manifest.'
}
Test-Case 'Reused snapshots carry verified downloads and their original fetch dates' {
    $source = Join-Path $testRoot 'reuse-source'
    Save-TestFetch $source
    # Only the sources missing from the reused snapshot may reach the network.
    function Get-CatalogResponse([string]$Url) { throw "Unexpected download: $Url" }
    $run = Join-Path $testRoot 'reuse-run'
    Save-CatalogFetch $run $source
    $manifest = Read-CatalogJson (Join-Path $run 'fetch.json')
    Assert ($manifest.sources.Count -eq $script:CatalogSources.Count) 'Reuse produced an incomplete manifest.'
    foreach ($name in $script:CatalogSources.Keys) {
        $carried = $manifest.sources[$name]
        Assert ($carried.reusedFrom -ceq (Split-Path $source -Leaf)) "Source $name was not marked as reused."
        Assert ($carried.fetchedAt -ceq '2026-09-09T00:00:00Z' -or $carried.fetchedAt -ceq '2026-09-09T01:00:00Z') "Source $name lost its original retrieval date."
        foreach ($file in @($carried.files)) {
            Assert ((Get-CatalogHash (Get-CatalogRelativePath $run $file.path)) -ceq $file.sha256) "Reused file $($file.path) did not match its recorded hash."
        }
    }
    # A complete reused snapshot must still replay without touching the network.
    $records = Read-CatalogFetch $run
    Assert ($records.Count -gt 0) 'A reused snapshot did not replay.'
    Assert-Throws { Save-CatalogFetch $run $source } 'new run directory'
}
Test-Case 'Reuse rejects altered caches and drops sources that are no longer registered' {
    $source = Join-Path $testRoot 'reuse-altered-source'
    Save-TestFetch $source
    [IO.File]::AppendAllText((Join-Path $source 'raw/switchoddities/page-1.json'), ' ')
    function Get-CatalogResponse([string]$Url) { throw "Unexpected download: $Url" }
    Assert-Throws { Save-CatalogFetch (Join-Path $testRoot 'reuse-altered-run') $source } 'checksum mismatch'
    $retired = Join-Path $testRoot 'reuse-retired-source'
    Save-TestFetch $retired
    $manifest = Read-CatalogJson (Join-Path $retired 'fetch.json')
    $manifest.sources['retiredsource'] = [ordered]@{ files = @([ordered]@{ path = 'raw/switchoddities/page-1.json'; sha256 = Get-CatalogHash (Join-Path $retired 'raw/switchoddities/page-1.json') }) }
    Write-CatalogJson (Join-Path $retired 'fetch.json') $manifest
    $run = Join-Path $testRoot 'reuse-retired-run'
    Save-CatalogFetch $run $retired
    $result = Read-CatalogJson (Join-Path $run 'fetch.json')
    Assert (-not $result.sources.Contains('retiredsource')) 'An unregistered source was carried into the new snapshot.'
    Assert ($result.sources.Count -eq $script:CatalogSources.Count) 'Dropping a retired source changed the registered set.'
}
Test-Case 'Failed replay invalidates earlier promotion receipt' {
    $root = Join-Path $testRoot 'failed-replay-root'
    $run = Join-Path $testRoot 'failed-replay-run'
    Save-TestFetch $run
    Save-CatalogCandidate $root $run
    [IO.File]::AppendAllText((Join-Path $run 'raw/switchoddities/page-1.json'), ' ')
    Assert-Throws { Save-CatalogCandidate $root $run } 'checksum mismatch'
    Assert (-not (Test-Path -LiteralPath (Join-Path $run 'candidate/receipt.json'))) 'Stale receipt remained.'
}
Test-Case 'Ordinary promotion write failure restores earlier replacements' {
    $root = Join-Path $testRoot 'rollback-root'
    $run = Join-Path $testRoot 'rollback-run'
    Save-TestFetch $run
    Save-CatalogCandidate $root $run
    Publish-CatalogCandidate $root $run
    $state = Read-CatalogState $root
    $id = $state.catalogs.switches.entries[0].id
    $state.overrides.entries["switches/$id"] = [ordered]@{ name = 'Changed switch name' }
    Write-CatalogJson (Join-Path $root 'Scripts/Catalogs/overrides.json') $state.overrides
    Save-CatalogCandidate $root $run
    $switchPath = Join-Path $root 'Assets/Catalogs/switches.json'
    $keycapPath = Join-Path $root 'Assets/Catalogs/keycaps.json'
    $before = Get-CatalogHash $switchPath
    $attributes = [IO.File]::GetAttributes($keycapPath)
    try {
        [IO.File]::SetAttributes($keycapPath, ($attributes -bor [IO.FileAttributes]::ReadOnly))
        Assert-Throws { Publish-CatalogCandidate $root $run } 'denied|read.only|access'
        Assert ((Get-CatalogHash $switchPath) -ceq $before) 'Earlier catalog replacement was not rolled back.'
    } finally { [IO.File]::SetAttributes($keycapPath, $attributes) }
}
Test-Case 'Large source omissions block an update instead of silently shrinking coverage' {
    $records = @(New-TestRecords)
    foreach ($n in 1..25) {
        $record = Copy-Value $records[0]
        $record.sourceId = [string](9000 + $n)
        $record.entry.name = "Distinct Switch $n"
        $records += $record
    }
    $state = (New-CatalogCandidate (Read-CatalogState (Join-Path $testRoot 'empty')) $records).state
    Assert-Throws { New-CatalogCandidate $state (New-TestRecords) } 'Suspicious source count drop'
}
Test-Case 'Only an explicit exclusion can remove a mistakenly accepted listing' {
    $state = New-TestState
    $records = New-TestRecords
    $records[1].excluded = 'New adapter filter'
    $next = New-CatalogCandidate $state $records
    Assert ($next.state.catalogs.keycaps.entries.Count -eq 2 -and $next.report.removals.Count -eq 0) 'Adapter filter deleted history.'
    $state.overrides.excluded['divinikey:101'] = 'Reviewed incorrect listing'
    $next = New-CatalogCandidate $state $records
    Assert ($next.state.catalogs.keycaps.entries.Count -eq 1 -and $next.report.removals.Count -eq 1) 'Explicit correction was not applied.'
}

Test-Case 'Each retailer vendor field is read for what that store puts in it' {
    function New-TestKeycapListing([string]$Title, [string]$Vendor) {
        [ordered]@{ id = 960; title = $Title; handle = 'example'; vendor = $Vendor; body_html = ''; options = @(); variants = @() }
    }
    $stocked = Convert-CatalogProduct (New-TestKeycapListing 'Example Set' 'Stocked') 'cannonkeys'
    Assert (-not $stocked.entry.Contains('brand') -and -not $stocked.entry.Contains('manufacturer')) 'A stock status became a company.'
    $made = Convert-CatalogProduct (New-TestKeycapListing 'Example Set' 'Signature Plastics') 'dailyclack'
    Assert ($made.entry.manufacturer -ceq 'Signature Plastics') 'A retailer maker attribution was lost.'
    foreach ($vendor in @('Artisan', 'NovelKeys', 'KBDfans')) {
        $entry = (Convert-CatalogProduct (New-TestKeycapListing 'Example Set' $vendor) 'dailyclack').entry
        Assert (-not $entry.Contains('manufacturer')) "$vendor was trusted as a maker."
    }
    # The four original retailers keep reading their vendor as the selling brand.
    Assert ((Convert-CatalogProduct (New-TestKeycapListing 'Example Set' 'PBTfans') 'divinikey').entry.brand -ceq 'PBTfans') 'Existing vendor handling changed.'
}
Test-Case 'Keycap titles reach the spelling the catalog already uses' {
    foreach ($pair in @(
        @{ title = 'GMK Blurple (CYL)'; name = 'GMK CYL Blurple' }
        @{ title = 'GMK Blurple (CYL) Bundle'; name = 'GMK CYL Blurple' }
        @{ title = 'CYL Finer Things R2'; name = 'GMK CYL Finer Things R2' }
        @{ title = 'Keyboard Science - Mio Yogurt'; name = 'Keyboard Science Mio Yogurt' }
        @{ title = 'GMK Beloved - KA2017 Revival'; name = 'GMK Beloved - KA2017 Revival' }
        @{ title = 'Qtuo Studio - Magic Bunny'; name = 'Qtuo Studio - Magic Bunny' }
    )) {
        Assert ((Get-CatalogKeycapTitle $pair.title) -ceq $pair.name) "$($pair.title) became $(Get-CatalogKeycapTitle $pair.title)"
    }
    Assert ((Get-CatalogMatchName (Get-CatalogKeycapTitle 'CYL Finer Things R2')) -ceq (Get-CatalogMatchName 'GMK Finer Things R2')) 'A CYL listing missed its existing set.'
    foreach ($title in @('GMK Child Kit Mega Listing', 'Example 40s Kit Collection', 'GMK Leftover Sale', 'Example ABS Leftovers')) {
        $listing = [ordered]@{ id = 961; title = $title; handle = 'x'; vendor = ''; body_html = ''; options = @(); variants = @() }
        Assert ((Convert-CatalogProduct $listing 'novelkeys').excluded -match 'several products') "$title was accepted as one product."
    }
    foreach ($title in @('Example Switches', '[Pre-Order] Example Systems Faceplates')) {
        $listing = [ordered]@{ id = 961; title = $title; handle = 'x'; vendor = ''; body_html = ''; options = @(); variants = @() }
        Assert ((Convert-CatalogProduct $listing 'keygem').excluded -match 'Not a keycap') "$title was accepted as keycaps."
    }
    foreach ($title in @('Example Keycaps B-Stock', 'Example Keycaps (B-Stock)')) {
        $listing = [ordered]@{ id = 961; title = $title; handle = 'x'; vendor = ''; body_html = ''; options = @(); variants = @() }
        $record = Convert-CatalogProduct $listing 'keygem'
        Assert (-not $record.excluded -and $record.entry.name -ceq 'Example') "$title did not reach its clean name."
    }
    Assert ((Get-CatalogSpecimenReason 'CXA Keycaps - Trial Set') -match 'Sample') 'A trial set was accepted.'
    Assert ((Get-CatalogSpecimenReason 'BOW Keycaps - Display Unit') -match 'display') 'A display unit was accepted.'
}
Test-Case 'Labels accept a colon or a dash, and company credits drop trademark styling' {
    $pattern = 'Manufactured by|Manufacturer(?:[ \t]*:|[ \t]+)'
    foreach ($line in @('Manufacturer: GMK', 'Manufacturer - GMK', "Manufacturer $([char]0x2014) GMK", "Manufacturer $([char]0x2013) GMK")) {
        Assert ((Get-CatalogLabel $line $pattern) -ceq 'GMK') "Label separator was kept in: $line"
    }
    Assert ((Get-CatalogLabel 'Designer: Jean-Luc' 'Designed by|Designer(?:[ \t]*:|[ \t]+)') -ceq 'Jean-Luc') 'A hyphen inside a value was cut.'
    Assert ((Get-CatalogCreditName ('PBTfans' + [char]0x2122)) -ceq 'PBTfans') 'A trademark sign survived in a company credit.'
    Assert ((Get-CatalogLubeFreeName "Example Linear $([char]0x2013) Dry") -ceq 'Example Linear') 'An en dash lube option was not removed.'
}
Test-Case 'Tester options are excluded and separated unlubed labels do not create models' {
    $tester = [ordered]@{ entry = [ordered]@{ name = 'Low Profile Keychron Optical - Low Profile Tester' }; excluded = $null }
    Set-CatalogSwitchProduct $tester
    Assert ($tester.excluded -match 'tester') 'A mixed tester variant was accepted as a switch model.'
    foreach ($label in @('Un-lubed', 'Un lubed', 'Unlubed')) {
        $name = Get-CatalogSwitchSuffixName "KFA Pink Robin Switches - $label (36)"
        Assert ((Get-CatalogLubeFreeName $name) -ceq 'KFA Pink Robin') "Purchase option survived: $label"
    }
    Assert ((Get-CatalogLubeFreeName 'NK Dry Black') -ceq 'NK Dry Black') 'The named Dry product line was altered.'
    Assert (-not (Get-CatalogSpecimenReason 'Cherry MX Brown')) 'An individual switch was excluded.'
}
Test-Case 'Factory defects and macro-pad hardware are not switch models' {
    foreach ($name in @("'Defective' C3 Kiwi", 'Factory Defect SP Star Meteor White', 'Punkshoo Melody Factory Errors', 'Keychron C100 8K Giant Custom Macro Pad')) {
        $record = [ordered]@{ entry = [ordered]@{ name = $name }; excluded = $null }
        Set-CatalogSwitchProduct $record
        Assert ($record.excluded) "Unwanted specimen or device accepted: $name"
    }
    $regular = [ordered]@{ entry = [ordered]@{ name = 'Punkshoo Melody' }; excluded = $null }
    Set-CatalogSwitchProduct $regular
    Assert (-not $regular.excluded) 'The regular switch was excluded with its defective specimens.'
    Assert (-not (Get-CatalogSpecimenReason 'Example Macro Pad Keycaps')) 'The hardware filter leaked into keycap specimen filtering.'
}
Test-Case 'Spellings of one value agree, and only a real difference conflicts' {
    $records = New-TestRecords
    $first = Copy-Value $records[1]; $first.sourceId = 'spelling-a'; $first.entry = [ordered]@{ name = 'Spelling Example'; designer = 'biip' }
    $state = (New-CatalogCandidate (Read-CatalogState (Join-Path $testRoot 'spelling-empty')) @($records[0], $first)).state
    $second = Copy-Value $first; $second.sourceId = 'spelling-b'; $second.entry.designer = 'BIIP'
    $state.overrides.bindings['divinikey:spelling-b'] = 'spelling-example'
    $result = New-CatalogCandidate $state @($records[0], $first, $second)
    $entry = @($result.state.catalogs.keycaps.entries | Where-Object { $_.id -ceq 'spelling-example' })[0]
    Assert ($result.report.conflicts.Count -eq 0 -and $entry.designer -ceq 'biip') 'A case-only variant restyled the accepted credit or reported a conflict.'
    $second.entry.designer = 'Someone Else'
    Assert ((New-CatalogCandidate $state @($records[0], $first, $second)).report.conflicts.Count -eq 1) 'A genuine disagreement passed silently.'
}
Test-Case 'Keycap names keep their Latin form and drop the translation' {
    # Built from code points so this file stays ASCII.
    function Get-TestScript([int[]]$Codes) { -join ($Codes | ForEach-Object { [char]$_ }) }
    $dracula = Get-TestScript 0x5FB7,0x53E4,0x62C9
    foreach ($pair in @(
        @{ name = "GMK Dracula R2 ${dracula}R2"; latin = 'GMK Dracula R2' }
        @{ name = "GMK Carbon R1 $dracula R1"; latin = 'GMK Carbon R1' }
        @{ name = "GMK CYL Kaiju CYL$dracula"; latin = 'GMK CYL Kaiju' }
        @{ name = "PBTfans ${dracula}RUNNER"; latin = 'PBTfans RUNNER' }
        @{ name = "DMK $dracula (In Former Days$([char]0xFF09)"; latin = 'DMK In Former Days' }
        @{ name = "DCS $(Get-TestScript 0xD64D,0xAC8C) (Red Crab)"; latin = 'DCS Red Crab' }
        # Two translated names joined by a slash leave no stray slash behind.
        @{ name = "GMK Cyrillic WoB Beige $dracula/$dracula"; latin = 'GMK Cyrillic WoB Beige' }
        @{ name = "GMK Crimson Royal Cadet $dracula / $dracula"; latin = 'GMK Crimson Royal Cadet' }
        # A repeated word the translation did not introduce is part of the name.
        @{ name = "GMK Bora Bora $dracula"; latin = 'GMK Bora Bora' }
    )) {
        Assert ((Get-CatalogLatinName $pair.name) -ceq $pair.latin) "Translation handling produced '$(Get-CatalogLatinName $pair.name)' for $($pair.latin)."
    }
    Assert ((Get-CatalogLatinName $dracula) -ceq $dracula) 'A name with no Latin form was emptied.'
    $keycap = Get-NormalizedCatalogEntry ([ordered]@{ name = "GMK Dracula R2 ${dracula}R2" }) 'keycaps'
    Assert ($keycap.name -ceq 'GMK Dracula R2' -and (Get-CatalogSlug $keycap.name) -ceq 'gmk-dracula-r2') 'The translation reached the name or a doubled ID.'
    $switch = Get-NormalizedCatalogEntry ([ordered]@{ name = "$dracula Studio x JWK Blue Lotus" }) 'switches'
    Assert ($switch.name -ceq "$dracula Studio x JWK Blue Lotus") 'A switch studio name was stripped.'
}
Test-Case 'Rounds, joiners, and accents match however a store writes them' {
    foreach ($pair in @(@('GMK Dualshot 2', 'GMK CYL Dualshot R2'), @('ePBT Acid House & Sweet Girl', 'ePBT Acid House and Sweet Girl'),
            @('GMK Beta & JS R2', 'GMK Beta / JS R2'), ("GMK Jam$([char]0xF3)n r2", 'GMK Jamon R2'),
            @('GMK Dracula V2.0', 'GMK Dracula R2'), @('GMK CYL Taiga 2.0', 'GMK Taiga R2'), @('GMK Oblivion V3.1', 'GMK Oblivion R3.1'),
            @('GMK DualShot R1', 'GMK Dualshot'), @('KTT Mallo V1', 'KTT Mallo'))) {
        Assert ((Get-CatalogMatchName $pair[0]) -ceq (Get-CatalogMatchName $pair[1])) "$($pair[0]) and $($pair[1]) did not match."
    }
    # Different rounds, and numbers that are not rounds, stay distinct.
    Assert ((Get-CatalogMatchName 'GMK Dualshot R1') -cne (Get-CatalogMatchName 'GMK Dualshot R2')) 'Different rounds matched.'
    Assert ((Get-CatalogMatchName 'GMK Oblivion V3.1') -cne (Get-CatalogMatchName 'GMK Oblivion V3.2')) 'Different point versions matched.'
    Assert ((Get-CatalogMatchName 'GMK Extended 2048') -cne (Get-CatalogMatchName 'GMK Extended R2048')) 'A four-digit number was read as a round.'
    # Source identities hash the published name, so existing bindings must not move.
    Assert ((Get-CatalogMatchName 'Switch Example 2' -SourceIdentity) -cne (Get-CatalogMatchName 'Switch Example R2' -SourceIdentity)) 'Round folding reached a source identity.'
}
Test-Case 'Colorway abbreviations match whole names without changing identities' {
    foreach ($pair in @(@('KKB WoB', 'KeyKobo White on Black'), @('PBTfans BoW', 'PBTfans Black on White (BoW)'),
            @('GMK MTNU WoB', 'GMK MTNU WoB (White on Black)'), @('Keychron BoW', 'Keychron Black-on-White - BoW'),
            @('GMK WoB R2', 'GMK White on Black (WoB) R2'))) {
        Assert ((Get-CatalogMatchName $pair[0]) -ceq (Get-CatalogMatchName $pair[1])) 'Expanded colorway failed to match.'
    }
    foreach ($pair in @(@('GMK WoB', 'GMK BoW'), @('GMK WoB', 'KKB WoB'), @('GMK WoB', 'GMK MTNU WoB'),
            @('GMK WoB R2', 'GMK White on Black R3'), @('GMK WoB', 'GMK WoB Addon'), @('KKB RainBoW', 'KKB RainWoB'))) {
        Assert ((Get-CatalogMatchName $pair[0]) -cne (Get-CatalogMatchName $pair[1])) 'A meaningful product distinction was lost.'
    }
    Assert ((Get-CatalogMatchName 'RainBoW') -ceq 'rainbow') 'An abbreviation inside a model name was expanded.'
    Assert ((Get-CatalogMatchName 'GMK WoB' -SourceIdentity) -cne (Get-CatalogMatchName 'GMK White on Black' -SourceIdentity)) 'Colorway folding reached source identities.'
}
Test-Case 'New stores preserve kit identities, profiles, and production roles' {
    $product = [ordered]@{ id = 1; title = 'matcha marshmallow keycaps'; handle = 'matcha-marshmallow'; vendor = 'osume'; body_html = ''; product_type = 'marshmallow keycaps'; variants = @() }
    $record = Convert-CatalogProduct $product 'osume'
    Assert ($record.entry.name -ceq 'osume matcha marshmallow' -and $record.entry.profile -ceq 'Marshmallow') 'Osume lost its brand or distinct profile.'
    $product.title = 'matcha novelty kit'
    $record = Convert-CatalogProduct $product 'osume'
    Assert ($record.entry.name -ceq 'osume matcha novelty kit' -and -not $record.excluded) 'A named add-on was collapsed or excluded.'
    $product.title = '(In Stock) GMK CYL Example R2 Keyset'
    $product.vendor = 'proto[Typist] Keyboards'
    $record = Convert-CatalogProduct $product 'prototypist'
    Assert ($record.entry.name -ceq 'GMK CYL Example R2' -and -not $record.entry.Contains('brand')) 'Stock wording or retailer became product identity.'
    $product.title = '(In Stock) KAM Soda Squid Deskmats'
    $record = Convert-CatalogProduct $product 'prototypist'
    Assert ($record.excluded) 'A deskmat-only listing entered the keycap catalog.'
    $product.title = '(In Stock) Infinikey Marshmallow & Deskmats'
    $product.body_html = '<p>Manufactured by Infinikey.</p>'
    $product.variants = @([ordered]@{ title = 'Base Kit' }, [ordered]@{ title = 'Deskmat' })
    $record = Convert-CatalogProduct $product 'prototypist'
    Assert ($record.entry.name -ceq 'Infinikey Marshmallow' -and -not $record.excluded) 'A keyset with optional deskmat was lost.'
    Assert ($record.entry.manufacturer -ceq 'Infinikey' -and $record.notes -match 'Base Kit; Deskmat') 'Maker spelling or kit options were lost.'
    $product.title = '(In Stock) KAM Sewing Tin'
    $product.body_html = '<p>Manufacturer minimums are based on key counts.</p>'
    $record = Convert-CatalogProduct $product 'prototypist'
    Assert (-not $record.entry.Contains('manufacturer')) 'MOQ prose became a manufacturer.'
    $product.body_html = '<p>Manufactured by Signature Plastics in the USA.</p>'
    $record = Convert-CatalogProduct $product 'prototypist'
    Assert ($record.entry.manufacturer -ceq 'Signature Plastics') 'Country suffix became part of the maker name.'
    $product.title = 'Anthracite Keycaps'
    $product.body_html = '<ul><li>Cherry Profile</li><li>ABS &amp; PBT material blend tuned for sound.</li></ul>'
    $record = Convert-CatalogProduct $product 'mode'
    Assert ($record.entry.name -ceq 'Mode Anthracite' -and $record.entry.material -ceq 'ABS/PBT' -and $record.entry.profile -ceq 'Cherry') 'Mode blend specifications were lost.'
    Assert (-not $record.entry.Contains('manufacturer')) 'Mode was assigned an unverified manufacturer.'
    $product.title = 'KAM Soda Squid'
    $product.vendor = 'KeebsForAll'
    $record = Convert-CatalogProduct $product 'keebsforall'
    Assert (-not $record.entry.Contains('brand')) 'A multibrand store became the product brand.'
}
Test-Case 'Historical DCS group buys preserve their identities without invented variants' {
    foreach ($name in @('DCS Round 1', 'DCS Round 3 and 4')) {
        $entry = Get-NormalizedCatalogEntry ([ordered]@{ name = $name; designer = 'Original designer'; profile = 'DCS' }) 'keycaps'
        Assert ($entry.name -ceq $name -and -not $entry.Contains('variants')) 'Historical DCS identity became a release list.'
        Assert ((ConvertTo-CatalogJson (Get-NormalizedCatalogEntry $entry 'keycaps')) -ceq (ConvertTo-CatalogJson $entry)) 'DCS normalization was not stable.'
    }
}
Test-Case 'R0/R5 sculpt kit is not expanded into production rounds' {
    $labels = Get-CatalogVariantLabels @('40s', 'Colevrak+', 'R0/R5') 'GMK WoB 40s, Colevrak+, R0/R5'
    Assert (($labels -join ',') -ceq '40s,Colevrak+,R0/R5') 'Sculpt rows became invented rounds.'
    $labels = Get-CatalogVariantLabels @('R0/R5', 'R2')
    Assert (($labels -join ',') -ceq 'R1,R2,R0/R5') 'A sculpt kit interfered with an actual release label.'
}
Test-Case 'Cherry profile wording leaves names and credits while profile metadata survives' {
    $entry = Get-NormalizedCatalogEntry ([ordered]@{ name = 'Example Cherry profile'; profile = 'Cherry'; designer = 'OneCreativeMind and Alas, this set consists of Cherry Profile, Dye-sublimated PBT keycaps.' }) 'keycaps'
    Assert ($entry.name -ceq 'Example' -and $entry.profile -ceq 'Cherry' -and $entry.designer -ceq 'OneCreativeMind and Alas') 'Profile wording or trailing designer prose survived.'
    $entry = Get-NormalizedCatalogEntry ([ordered]@{ name = 'NicePBT Deep North'; designer = 'Liv, NicePBT Deep North consists of Cherry Profile, dye-sub keycaps.' }) 'keycaps'
    Assert ($entry.designer -ceq 'Liv') 'Named-set designer prose survived.'
    $entry = Get-NormalizedCatalogEntry ([ordered]@{ name = 'Example'; designer = 'PWade3 this set consists of Cherry Profile, dye-sub keycaps.' }) 'keycaps'
    Assert ($entry.designer -ceq 'PWade3') 'Designer prose without a comma survived.'
}
Test-Case 'Outemu Sky keeps only reviewed variants without inferred predecessors' {
    $entry = Get-NormalizedCatalogEntry ([ordered]@{ id = 'outemu-sky'; name = 'Outemu Sky'; variants = @('V2.1', 'V2.2', '62g', '68g', '75g', '80g') }) 'switches'
    Assert (($entry.variants -join ',') -ceq '62g,68g,75g,80g,V2.1,V2.2') 'Unverified Sky versions were inferred.'
}
Test-Case 'New open-slot source records cannot reintroduce removed switches' {
    $state = New-TestState
    $records = @(New-TestRecords)
    foreach ($label in @('Open Slot', 'Open-Slot')) {
        $record = Copy-Value $records[0]
        $record.sourceId = "new-$label"
        $record.entry.name = "Example ($label)"
        $records += $record
    }
    $next = New-CatalogCandidate $state $records
    Assert ((ConvertTo-CatalogJson $state.catalogs) -ceq (ConvertTo-CatalogJson $next.state.catalogs)) 'An open-slot switch was accepted.'
}
Test-Case 'Simple variant labels survive normalization and no-op replay' {
    $state = New-TestState
    $entry = $state.catalogs.switches.entries[0]
    $entry.variants = @('62g', '67g')
    $entry.switchFamily = 'MX'
    $entry.isLowProfile = $true
    $state.overrides.entries["switches/$($entry.id)"] = [ordered]@{ variants = @('62g', '67g') }
    $next = (New-CatalogCandidate $state (New-TestRecords)).state
    $actual = $next.catalogs.switches.entries[0]
    Assert ($actual.variants -is [array] -and ($actual.variants -join ',') -ceq '62g,67g') 'Variant list was flattened or discarded.'
    Assert ($actual.switchFamily -ceq 'MX' -and $actual.isLowProfile) 'Existing family/form-factor metadata was lost.'
    $again = (New-CatalogCandidate $next (New-TestRecords)).state
    Assert ((ConvertTo-CatalogJson $again) -ceq (ConvertTo-CatalogJson $next)) 'No-op replay changed variants or version.'
    $keycap = $state.catalogs.keycaps.entries[0]
    $keycap.variants = @('R1', 'R2')
    Test-CatalogState $state
}
Test-Case 'Variant labels reject nested data, empty labels, and duplicates' {
    foreach ($invalid in @('R1', @(), @(''), @(' R1'), @('R1', 'r1'), @([ordered]@{ id = 'r1' }))) {
        $state = New-TestState
        $state.catalogs.switches.entries[0].variants = $invalid
        Assert-Throws { Test-CatalogState $state } 'Invalid.*variant'
    }
    $state = New-TestState
    $state.catalogs.switches.entries[0].isLowProfile = 'true'
    Assert-Throws { Test-CatalogState $state } 'Invalid isLowProfile'
    $state.catalogs.switches.entries[0].isLowProfile = $false
    Assert-Throws { Test-CatalogState $state } 'Invalid isLowProfile'
}
Test-Case 'Source variant lists union without expanding combinations or losing retired labels' {
    $state = New-TestState
    $entry = $state.catalogs.switches.entries[0]
    $entry.variants = @('R1 / 62g')
    $records = New-TestRecords
    $records[0].entry.variants = @('R2 / 67g')
    $next = (New-CatalogCandidate $state $records).state
    Assert (($next.catalogs.switches.entries[0].variants -join ',') -ceq 'R1 / 62g,R2 / 67g') 'Variant combinations were lost or invented.'
    $records[0].entry.variants = @('r1 / 62g')
    $again = (New-CatalogCandidate $next $records).state
    Assert ($again.catalogs.switches.entries[0].variants.Count -eq 2) 'Case-only variant duplicated a label.'
}
Test-Case 'Reviewed switch singleton omissions survive source replay' {
    $state = New-TestState
    $entry = $state.catalogs.switches.entries[0]
    $state.overrides.entries["switches/$($entry.id)"] = [ordered]@{ variants = $null }
    $records = New-TestRecords
    $records[0].entry.variants = @('Clicky')
    $next = (New-CatalogCandidate $state $records).state
    Assert (-not $next.catalogs.switches.entries[0].Contains('variants')) 'A removed singleton returned from a source.'
    $again = (New-CatalogCandidate $next $records).state
    Assert ((ConvertTo-CatalogJson $again) -ceq (ConvertTo-CatalogJson $next)) 'Singleton omission was not stable.'
}
Test-Case 'Release labels fill integer predecessors without changing profiles or weights' {
    $labels = Get-CatalogVariantLabels @('r3', 'v2', '62g', 'R3')
    Assert (($labels -join ',') -ceq 'R1,R2,R3,V1,V2,62g') 'Releases were not normalized, expanded, and deduplicated.'
    $labels = Get-CatalogVariantLabels @('r2.5 / 67g')
    Assert (($labels -join ',') -ceq 'R1,R2,R2.5,R2.5 / 67g') 'Decimal release or weight combinations were fabricated.'
    $entry = Get-NormalizedCatalogEntry ([ordered]@{ id = 'sa-r3-1976'; name = 'SA-R3 1976'; profile = 'SA-R3'; variants = @('r2') }) 'keycaps'
    Assert ($entry.name -ceq 'SA-R3 1976' -and $entry.profile -ceq 'SA-R3' -and ($entry.variants -join ',') -ceq 'R1,R2') 'Profile R3 became a release.'
    $entry = Get-NormalizedCatalogEntry ([ordered]@{ id = 'plain'; name = 'SA-R3 1976'; profile = 'SA-R3' }) 'keycaps'
    Assert (-not $entry.Contains('variants')) 'A profile alone invented release variants.'
    Assert ((Get-CatalogReleaseName 'Example r2 / version 3 / Round 4') -ceq 'Example R2 / V3 / R4') 'Release casing was not canonicalized.'
}
Test-Case 'CRP rounds remain separate and sculpt kits do not expand into releases' {
    $records = @(
        [ordered]@{ source = 'dailyclack'; sourceId = 'crp-test-6'; kind = 'keycaps'; url = 'https://example.com/r6'; excluded = $null; entry = [ordered]@{ name = 'Hammerworks CRP Round 6'; variants = @('r0', 'r5', 'Numpad') } },
        [ordered]@{ source = 'dailyclack'; sourceId = 'crp-test-7'; kind = 'keycaps'; url = 'https://example.com/r7'; excluded = $null; entry = [ordered]@{ name = 'CRP r7'; variants = @('Desko Black', 'R5A', 'R1 Accent Blue') } }
    )
    $next = (New-CatalogCandidate (New-TestState) $records).state
    $r6 = @($next.catalogs.keycaps.entries | Where-Object { $_.id -eq 'crp-r6' })[0]
    $r7 = @($next.catalogs.keycaps.entries | Where-Object { $_.id -eq 'crp-r7' })[0]
    Assert ($r6.name -ceq 'CRP R6' -and ($r6.variants -join ',') -ceq 'Numpad,R0,R5') 'CRP sculpt labels were lost or expanded.'
    Assert (($r7.variants -join ',') -ceq 'Desko Black,R1 Accent Blue,R5A') 'Round number contaminated the kit list.'
    $again = (New-CatalogCandidate $next $records).state
    Assert ((ConvertTo-CatalogJson $again) -ceq (ConvertTo-CatalogJson $next)) 'CRP round replay changed the catalog.'
    $old = Get-NormalizedCatalogEntry ([ordered]@{ name = 'CRP Hammerworks r1' }) 'keycaps'
    Assert ($old.name -ceq 'CRP R1' -and -not $old.Contains('variants')) 'Unknown old-round kits were invented.'
    $other = Get-NormalizedCatalogEntry ([ordered]@{ name = 'Example R3' }) 'keycaps'
    Assert (($other.variants -join ',') -ceq 'R1,R2,R3') 'CRP exception leaked to other products.'
}
Test-Case 'Keyboard compatibility models do not invent keycap releases' {
    foreach ($name in @('KBParadise ALPS V60 Vintage Blank', 'KBParadise ALPS V80 Vintage', 'KBParadise MX V60 Black Blank', 'Topre Realforce R3 Replacement Keycaps')) {
        $entry = Get-NormalizedCatalogEntry ([ordered]@{ id = 'model-test'; name = $name }) 'keycaps'
        Assert ($entry.name -ceq $name -and -not $entry.Contains('variants')) 'Keyboard model invented release variants.'
        $entry = Get-NormalizedCatalogEntry ([ordered]@{ id = 'model-test'; name = $name; variants = @('White', 'Black') }) 'keycaps'
        Assert (($entry.variants -join ',') -ceq 'Black,White') 'Model normalization changed literal choices.'
    }
    foreach ($release in @('V10', 'R10')) {
        $entry = Get-NormalizedCatalogEntry ([ordered]@{ id = 'release-test'; name = "Example $release" }) 'keycaps'
        Assert ($entry.variants.Count -eq 10 -and $entry.variants -contains $release) 'Model exception suppressed a genuine release.'
    }
}
Test-Case 'C64 rounds preserve literal kit lists without release variants' {
    $records = @(1, 2 | ForEach-Object {
        [ordered]@{ source = 'dailyclack'; sourceId = "c64-test-$_"; kind = 'keycaps'; url = 'https://example.com/c64'; excluded = $null; entry = [ordered]@{ name = "Hammerworks CRP C64 Round $_"; designer = 'BUGER.WORK'; variants = @('C64 Alphas', 'Numpad') } }
    })
    $next = (New-CatalogCandidate (New-TestState) $records).state
    foreach ($round in @(1, 2)) {
        $entry = @($next.catalogs.keycaps.entries | Where-Object { $_.id -eq "crp-c64-r$round" })[0]
        Assert ($entry.name -ceq "CRP C64 R$round" -and $entry.designer -ceq 'BUGER.WORK') 'C64 identity or credit changed.'
        Assert (($entry.variants -join ',') -ceq 'C64 Alphas,Numpad') 'C64 round number became a kit.'
    }
    $again = (New-CatalogCandidate $next $records).state
    Assert ((ConvertTo-CatalogJson $again) -ceq (ConvertTo-CatalogJson $next)) 'C64 replay changed the catalog.'
}
Test-Case 'Reviewed historical keycap provenance survives absent feeds' {
    $state = New-TestState
    $record = [ordered]@{ source = 'reviewedkeycaps'; sourceId = 'crp-r1'; kind = 'keycaps'; url = 'https://example.com/history'; notes = 'Reviewed historical CRP round evidence; kit list unavailable.'; excluded = $null; entry = [ordered]@{ name = 'CRP R1' } }
    $next = (New-CatalogCandidate $state @($record)).state
    $again = (New-CatalogCandidate $next @()).state
    Assert ((ConvertTo-CatalogJson $again) -ceq (ConvertTo-CatalogJson $next)) 'Missing live feed lost reviewed history.'
    $binding = @($next.bindings | Where-Object { $_.source -eq 'reviewedkeycaps' })[0]
    $binding.Remove('notes')
    Assert-Throws { Test-CatalogState $next } 'Invalid reviewed source'
}
Test-Case 'Catalog scripts stay ASCII so Windows PowerShell reads them as written' {
    # Without a byte order mark, Windows PowerShell decodes a script in the ANSI code page,
    # where an em dash contains a closing curly quote that ends a string early.
    $scripts = @(Get-ChildItem -LiteralPath (Split-Path $PSScriptRoot -Parent) -Recurse -Filter '*.ps1') + @(Get-Item (Join-Path (Split-Path (Split-Path $PSScriptRoot -Parent) -Parent) 'Import-Catalogs.ps1'))
    foreach ($script in $scripts) {
        $text = [IO.File]::ReadAllText($script.FullName, [Text.Encoding]::GetEncoding(28591))
        Assert ($text -notmatch '[^\x00-\x7F]') "$($script.Name) contains a non-ASCII character."
    }
}

# A locked file only warns, so cleanup never changes the reported result.
try { Remove-Item -LiteralPath $testRoot -Recurse -Force }
catch { Write-Warning "Test files could not be removed from ${testRoot}: $($_.Exception.Message)" }
Write-Host "$script:Passed test groups passed; $($script:Failures.Count) failed."
if ($script:Failures.Count) { throw ($script:Failures -join "`n") }
