with open('c:\\AI_noor\\NoorPlatform_Fixed (3)\\NoorPlatform_Fixed\\NoorPlatform\\NoorPlatform.Api\\wwwroot\\index.html', 'r', encoding='utf-8') as f:
    content = f.read()

start_marker = "    <!-- ==================== NEW PAGES ==================== -->"
end_marker = "    <!-- ==================== NEW MODALS ==================== -->"

if start_marker in content and end_marker in content:
    start_idx = content.find(start_marker)
    end_idx = content.find(end_marker)
    
    pages_block = content[start_idx:end_idx]
    
    new_content = content[:start_idx] + content[end_idx:]
    
    insert_marker = "    </main>"
    insert_idx = new_content.find(insert_marker)
    
    if insert_idx != -1:
        new_content = new_content[:insert_idx] + pages_block + new_content[insert_idx:]
        
        with open('c:\\AI_noor\\NoorPlatform_Fixed (3)\\NoorPlatform_Fixed\\NoorPlatform\\NoorPlatform.Api\\wwwroot\\index.html', 'w', encoding='utf-8') as f:
            f.write(new_content)
        print('Success')
    else:
        print('Could not find </main>')
else:
    print('Could not find markers')
